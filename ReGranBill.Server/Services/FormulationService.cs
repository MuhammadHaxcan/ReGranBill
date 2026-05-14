using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Formulations;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class FormulationService : IFormulationService
{
    private const decimal RatioTolerance = 0.01m;

    private readonly AppDbContext _db;

    public FormulationService(AppDbContext db) => _db = db;

    public async Task<List<FormulationDto>> GetAllAsync()
    {
        var formulations = await _db.Formulations
            .Include(f => f.Lines).ThenInclude(l => l.Account)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return formulations.Select(MapToDto).ToList();
    }

    public async Task<FormulationDto?> GetByIdAsync(int id)
    {
        var formulation = await _db.Formulations
            .Include(f => f.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(f => f.Id == id);

        return formulation == null ? null : MapToDto(formulation);
    }

    public async Task<FormulationDto> CreateAsync(CreateFormulationRequest request, int userId)
    {
        var accounts = await ValidateAsync(request);

        var formulation = new Formulation
        {
            Name = request.Name.Trim(),
            Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description),
            BaseInputKg = request.BaseInputKg,
            IsActive = request.IsActive,
            CreatedBy = userId
        };

        var sortOrder = 0;
        foreach (var line in request.Lines)
        {
            formulation.Lines.Add(new FormulationLine
            {
                LineKind = line.LineKind,
                AccountId = line.AccountId,
                AmountPerBase = line.AmountPerBase,
                BagsPerBase = line.BagsPerBase,
                SortOrder = ++sortOrder
            });
        }

        _db.Formulations.Add(formulation);
        await _db.SaveChangesAsync();

        return (await GetByIdAsync(formulation.Id))!;
    }

    public async Task<FormulationDto?> UpdateAsync(int id, CreateFormulationRequest request)
    {
        var formulation = await _db.Formulations
            .Include(f => f.Lines)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (formulation == null) return null;

        await ValidateAsync(request, ignoreNameFromId: id);

        formulation.Name = request.Name.Trim();
        formulation.Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description);
        formulation.BaseInputKg = request.BaseInputKg;
        formulation.IsActive = request.IsActive;
        formulation.UpdatedAt = DateTime.UtcNow;

        _db.FormulationLines.RemoveRange(formulation.Lines);
        formulation.Lines.Clear();

        var sortOrder = 0;
        foreach (var line in request.Lines)
        {
            formulation.Lines.Add(new FormulationLine
            {
                LineKind = line.LineKind,
                AccountId = line.AccountId,
                AmountPerBase = line.AmountPerBase,
                BagsPerBase = line.BagsPerBase,
                SortOrder = ++sortOrder
            });
        }

        await _db.SaveChangesAsync();
        return (await GetByIdAsync(formulation.Id))!;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var formulation = await _db.Formulations.FirstOrDefaultAsync(f => f.Id == id);
        if (formulation == null) return false;

        formulation.IsDeleted = true;
        formulation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ApplyFormulationResponse?> ApplyAsync(int id, decimal totalInputKg)
    {
        if (totalInputKg <= 0)
            throw new RequestValidationException("Total input kg must be greater than zero.");

        var formulation = await _db.Formulations
            .Include(f => f.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (formulation == null) return null;
        if (formulation.BaseInputKg <= 0)
            throw new RequestValidationException("Formulation base input kg must be greater than zero.");

        var factor = totalInputKg / formulation.BaseInputKg;

        AppliedLineDto Project(FormulationLine line) => new()
        {
            AccountId = line.AccountId,
            AccountName = line.Account?.Name,
            Qty = line.BagsPerBase.HasValue ? (int)Math.Round(line.BagsPerBase.Value * factor, MidpointRounding.AwayFromZero) : 0,
            WeightKg = VoucherHelpers.Round2(line.AmountPerBase * factor),
            SortOrder = line.SortOrder
        };

        var inputs = formulation.Lines.Where(l => l.LineKind == ProductionLineKind.Input).OrderBy(l => l.SortOrder).Select(Project).ToList();
        var outputs = formulation.Lines.Where(l => l.LineKind == ProductionLineKind.Output).OrderBy(l => l.SortOrder).Select(Project).ToList();
        var byproducts = formulation.Lines.Where(l => l.LineKind == ProductionLineKind.Byproduct).OrderBy(l => l.SortOrder).Select(Project).ToList();
        var shortageLine = formulation.Lines.FirstOrDefault(l => l.LineKind == ProductionLineKind.Shortage);

        var totalInput = inputs.Sum(l => l.WeightKg);
        var rhs = outputs.Sum(l => l.WeightKg) + byproducts.Sum(l => l.WeightKg) + (shortageLine == null ? 0 : VoucherHelpers.Round2(shortageLine.AmountPerBase * factor));
        var diff = VoucherHelpers.Round2(totalInput - rhs);

        AppliedShortageDto? shortage = null;
        if (shortageLine != null)
        {
            shortage = new AppliedShortageDto
            {
                AccountId = shortageLine.AccountId,
                AccountName = shortageLine.Account?.Name,
                WeightKg = VoucherHelpers.Round2(shortageLine.AmountPerBase * factor + diff)
            };
        }

        return new ApplyFormulationResponse
        {
            Inputs = inputs,
            Outputs = outputs,
            Byproducts = byproducts,
            Shortage = shortage
        };
    }

    private async Task<IReadOnlyDictionary<int, Account>> ValidateAsync(CreateFormulationRequest request, int? ignoreNameFromId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new RequestValidationException("Formulation name is required.");
        if (request.Name.Trim().Length > 120)
            throw new RequestValidationException("Formulation name is too long.");
        if (request.BaseInputKg <= 0)
            throw new RequestValidationException("Base input kg must be greater than zero.");

        var name = request.Name.Trim();
        var nameExists = await _db.Formulations
            .AnyAsync(f => f.Name == name && (ignoreNameFromId == null || f.Id != ignoreNameFromId));
        if (nameExists)
            throw new RequestValidationException("A formulation with this name already exists.");

        if (request.Lines.Count == 0)
            throw new RequestValidationException("Add at least one line to the formulation.");

        var inputSum = request.Lines.Where(l => l.LineKind == ProductionLineKind.Input).Sum(l => l.AmountPerBase);
        if (Math.Abs(inputSum - request.BaseInputKg) > RatioTolerance)
            throw new RequestValidationException($"Input amounts must sum to {request.BaseInputKg:0.##} kg (the base). Current total: {inputSum:0.##}.");

        var outputSum = request.Lines
            .Where(l => l.LineKind != ProductionLineKind.Input)
            .Sum(l => l.AmountPerBase);
        if (Math.Abs(outputSum - request.BaseInputKg) > RatioTolerance)
            throw new RequestValidationException($"Output + byproduct + shortage amounts must sum to {request.BaseInputKg:0.##} kg. Current total: {outputSum:0.##}.");

        var shortageLines = request.Lines.Count(l => l.LineKind == ProductionLineKind.Shortage);
        if (shortageLines > 1)
            throw new RequestValidationException("A formulation can only have a single shortage line.");

        foreach (var line in request.Lines)
        {
            if (line.AccountId <= 0)
                throw new RequestValidationException("Each formulation line must reference an account.");
            if (line.AmountPerBase < 0)
                throw new RequestValidationException("Amounts cannot be negative.");
        }

        var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        foreach (var line in request.Lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account))
                throw new RequestValidationException("One or more selected accounts are invalid.");

            switch (line.LineKind)
            {
                case ProductionLineKind.Input:
                    if (account.AccountType != AccountType.RawMaterial && account.AccountType != AccountType.Product)
                        throw new RequestValidationException($"Input account '{account.Name}' must be a Raw Material or Product.");
                    break;
                case ProductionLineKind.Output:
                case ProductionLineKind.Byproduct:
                    if (account.AccountType != AccountType.Product)
                        throw new RequestValidationException($"Output / byproduct account '{account.Name}' must be a Product.");
                    break;
                case ProductionLineKind.Shortage:
                    if (account.AccountType != AccountType.Expense)
                        throw new RequestValidationException($"Shortage account '{account.Name}' must be an Expense account.");
                    break;
            }
        }

        return accounts;
    }

    private static FormulationDto MapToDto(Formulation formulation) => new()
    {
        Id = formulation.Id,
        Name = formulation.Name,
        Description = formulation.Description,
        BaseInputKg = formulation.BaseInputKg,
        IsActive = formulation.IsActive,
        CreatedAt = formulation.CreatedAt,
        UpdatedAt = formulation.UpdatedAt,
        Lines = formulation.Lines.OrderBy(l => l.SortOrder).Select(l => new FormulationLineDto
        {
            Id = l.Id,
            LineKind = l.LineKind,
            AccountId = l.AccountId,
            AccountName = l.Account?.Name,
            AmountPerBase = l.AmountPerBase,
            BagsPerBase = l.BagsPerBase,
            SortOrder = l.SortOrder
        }).ToList()
    };
}
