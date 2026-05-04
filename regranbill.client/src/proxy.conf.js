const { env } = require('process');
const fs = require('fs');
const path = require('path');

loadDotEnv();

const target = env.API_PROXY_TARGET ??
  (env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] :
    env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
    'http://localhost:5298');

function loadDotEnv() {
  const probePaths = [
    path.resolve(__dirname, '../.env'),
    path.resolve(__dirname, '../../.env')
  ];

  for (const envPath of probePaths) {
    if (!fs.existsSync(envPath)) {
      continue;
    }

    const lines = fs.readFileSync(envPath, 'utf8').split(/\r?\n/);

    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line || line.startsWith('#')) {
        continue;
      }

      const separatorIndex = line.indexOf('=');
      if (separatorIndex <= 0) {
        continue;
      }

      const key = line.slice(0, separatorIndex).trim();
      const value = line.slice(separatorIndex + 1).trim().replace(/^"(.*)"$/, '$1');

      if (key && !env[key]) {
        env[key] = value;
      }
    }

    return;
  }
}

const PROXY_CONFIG = [
  {
    context: [
      "/api",
    ],
    target,
    secure: false,
    changeOrigin: true
  }
]

module.exports = PROXY_CONFIG;
