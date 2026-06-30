# Changelog

## [1.0.2](https://github.com/G-MAKROGLOU/PgMini/compare/v1.0.1...v1.0.2) (2026-06-30)


### 🐛 Bug Fixes

* add workflow_dispatch trigger for manual publish ([fb11c36](https://github.com/G-MAKROGLOU/PgMini/commit/fb11c361d3c47a63a58c8356abaa5220e9f2703a))
* add workflow_dispatch trigger for manual publish ([5524583](https://github.com/G-MAKROGLOU/PgMini/commit/5524583ea5ba8def0b4cccc91b378b812051f504))
* correct GitHub username in CI badge URL ([ac92e75](https://github.com/G-MAKROGLOU/PgMini/commit/ac92e7514b70168d26746d01503ac5cea4a2a839))
* correct GitHub username in CI badge URL ([9a054a2](https://github.com/G-MAKROGLOU/PgMini/commit/9a054a29ff0835fff7d9397018f9918a206f9d4d))
* trigger publish on release published event instead of tag push ([4a17f10](https://github.com/G-MAKROGLOU/PgMini/commit/4a17f100629a3f4b46182df4d2be90e1545ccef1))
* trigger publish on release published event instead of tag push ([0efbc4a](https://github.com/G-MAKROGLOU/PgMini/commit/0efbc4a3b8135eda4c5ec5471ade03c7e1c79a3b))

## [1.0.1](https://github.com/G-MAKROGLOU/PgMini/compare/v1.0.0...v1.0.1) (2026-06-30)


### 🐛 Bug Fixes

* derive package version from git tag instead of MinVer ([ad5b5a0](https://github.com/G-MAKROGLOU/PgMini/commit/ad5b5a0064e5b2c239b648b92853ee9ca749f889))
* use NuGet/login@v1 action for Trusted Publishing OIDC exchange


### ⚙️ CI/CD

* set up GitHub Actions publishing pipeline with NuGet Trusted Publishing (keyless OIDC via NuGet/login@v1)
* add automated release management via Release Please
* add CodeQL static analysis workflow
* add Trivy filesystem vulnerability scanning with SARIF upload
* add format check and NuGet vulnerability audit to CI
* bump GitHub Actions runners: actions/checkout@v7, actions/setup-dotnet@v5, actions/upload-artifact@v7
* bump CodeQL actions from v3 to v4


### 📦 Dependencies

* bump coverlet.collector from 6.0.0 to 10.0.1
* bump FluentAssertions from 6.12.0 to 8.10.0
* bump Microsoft.Extensions.DependencyInjection.Abstractions from 8.0.2 to 10.0.9
* bump Microsoft.Extensions.Logging from 8.0.1 to 10.0.9


### 📚 Documentation

* replace em-dashes with hyphens in README
* add package metadata to csproj (description, tags, repository URL)
