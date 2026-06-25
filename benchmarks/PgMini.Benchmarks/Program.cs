using BenchmarkDotNet.Running;

// Usage:
//   Set env var PGMINI_BENCH_CONNSTR to a PostgreSQL connection string, e.g.:
//     Host=localhost;Database=pgmini_bench;Username=postgres;Password=postgres
//
//   Run:
//     dotnet run -c Release -- [filter]
//
//   Examples:
//     dotnet run -c Release                   # all benchmarks
//     dotnet run -c Release -- --filter *Read*  # only read benchmarks
//     dotnet run -c Release -- --filter *Write* # only write benchmarks

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
