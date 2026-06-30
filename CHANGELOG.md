# Changelog

## [1.1.0](https://github.com/G-MAKROGLOU/PgMini/compare/v1.0.4...v1.1.0) (2026-06-30)


### ✨ Features

* add IDbClientProvider for single and multi-database client resolution ([154e62a](https://github.com/G-MAKROGLOU/PgMini/commit/154e62ad9f7a67ce2318d7bc6946f0636fface63))
* add IDbClientProvider for single and multi-database client resolution ([0a82e14](https://github.com/G-MAKROGLOU/PgMini/commit/0a82e14c43d16e020d16d1a6b7409c6ee901a2f5))

## [1.0.4](https://github.com/G-MAKROGLOU/PgMini/compare/v1.0.3...v1.0.4) (2026-06-30)


### 🐛 Bug Fixes

* skip MinVer during publish to prevent version override ([6e566d6](https://github.com/G-MAKROGLOU/PgMini/commit/6e566d6d30259810bc1d0366bfa2ce52d6a778a8))
* skip MinVer during publish to prevent version override ([a4c20b7](https://github.com/G-MAKROGLOU/PgMini/commit/a4c20b71a9218effc5597a3c38f71feedbf30311))

## [1.0.3](https://github.com/G-MAKROGLOU/PgMini/compare/v1.0.2...v1.0.3) (2026-06-30)


### 🐛 Bug Fixes

* guard expensive log arguments with IsEnabled checks (CA1873) ([6c26a02](https://github.com/G-MAKROGLOU/PgMini/commit/6c26a029a8c075ea9a180df271edc6bab59f977f))
* guard expensive log arguments with IsEnabled checks (CA1873) ([70e69c7](https://github.com/G-MAKROGLOU/PgMini/commit/70e69c71de43e5eb1453f4e3d980797d93431760))
* resolve version before build so MinVer is overridden at all steps ([3468aff](https://github.com/G-MAKROGLOU/PgMini/commit/3468aff9b6a2362c66a3c243a71cc4070634a1f7))
* resolve version before build so MinVer is overridden at all steps ([3cd6422](https://github.com/G-MAKROGLOU/PgMini/commit/3cd6422ec3182ff02c97f1d931a36a575e9b25c5))

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
