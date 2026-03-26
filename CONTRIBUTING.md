# Contributing to Feature Flags Service

Thank you for your interest in contributing! Any and all types of contributions are encouraged and welcome. This project
aims to provide a high-performance, consistent feature flagging solution.

## Code of Conduct

By participating in this project you must be respectful and professional in all interactions.

## How to Contribute

1. **Report Bugs**: Use GitHub Issues or email `adiv@diviant.co.uk` to report any bugs you find.
2. **Suggest Features**: Open an issue to discuss new feature ideas or improvements.
3. **Submit Pull Requests**:
    * Fork the repository
    * Create an appropriate branch name
    * Ensure your code follows the existing style
    * Include tests for new functionality
    * Submit a pull request with a clear description of changes
    * If possible, include benchmarks to show performance is the same or improved

## Development Environment

* **.NET 10+ SDK** is required
* **Docker** is used for infrastructure (PostgreSQL, Redis, Prometheus, Grafana)
* Use `docker compose -f infrastructure/compose.localhost.yaml up -d` to start dependencies
* Refer to [QUICKSTART.md](docs/QUICKSTART.md) for detailed setup instructions
* Distributed development environment is in progress

## Performance Standards

This project has strict performance requirements (e.g., p99 < 5ms at 30k RPS per core ideally). Please ensure any
changes to the evaluation hot-path are benchmarked.

> If the project has helped you in any way, but you do not have time to contribute, I would also be grateful for a star
> on the project or sharing it with your network. Thank you!