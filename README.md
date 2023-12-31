![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio-dark-bg.svg#gh-dark-mode-only "Foundatio")![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio.svg#gh-light-mode-only "Foundatio")

[![Build status](https://github.com/FoundatioFx/Foundatio.Repositories/workflows/Build/badge.svg)](https://github.com/FoundatioFx/Foundatio.Repositories/actions)
[![NuGet Version](http://img.shields.io/nuget/v/Foundatio.Repositories.svg?style=flat)](https://www.nuget.org/packages/Foundatio.Repositories/)
[![feedz.io](https://img.shields.io/badge/endpoint.svg?url=https%3A%2F%2Ff.feedz.io%2Ffoundatio%2Ffoundatio%2Fshield%2FFoundatio.Repositories%2Flatest)](https://f.feedz.io/foundatio/foundatio/packages/Foundatio.Repositories/latest/download)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)

Generic repository contract and implementations. Currently only implemented for Elasticsearch, but there are plans for other implementations.

# Features

- Simple document repository pattern
  - CRUD operations: Add, Save, Remove, Get
- Supports patch operations
  - JSON patch
  - Partial document patch
  - Painless script patch (Elasticsearch)
  - Can be applied in bulk using queries
- Async events that can be wired up and listened to outside of the repos
- Caching (real-time invalidated before save and stored in distributed cache)
- Message bus support (enables real-time apps sends messages like doc updated up to the client so they know to update the UI)
- Searchable that works with Foundatio.Parsers lib for dynamic querying, filtering, sorting and aggregations
- Document validation
- Document versioning
- Soft deletes
- Auto document created and updated dates
- Document migrations
- Elasticsearch implementation
  - Plan to add additional implementations (Postgres with Marten would be a good fit)
- Elasticsearch index configuration allows simpler and more organized configuration
  - Schema versioning
  - Parent child queries
  - Daily and monthly index strategies
- Supports different consistency models (immediate, wait or eventual)
  - Can be configured at the index type or individual query level
- Query builders used to make common ways of querying data easier and more portable between repo implementations
- Can still use raw Elasticsearch queries
- Field includes and excludes to make the response size smaller
- Field conditions query builder
- Paging including snapshot paging support
- Dynamic field resolution for using friendly names of dynamically generated fields
- Jobs for index maintenance, snapshots, reindex
- Strongly typed field access (using lambda expressions) to enable refactoring
