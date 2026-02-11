# Architecture

The architecture of this project follows the Clean Architecture. This document will provide an overview of the
architecture itself, the layers that make up the project, the reasoning behind the design decisions, and any other
relevant information. From this point forward, the term "project" will refer to individual projects within the
solution - with the solution being the service.

## Overview

In my Feature Flags Service, there are four layers: Domain, Application, Infrastructure, and Presentation. The
Presentation layer in this case is called Web.Api, which is the .NET Web API project. There is also an additional
project called Contracts, which contains common types, custom exception classes, and requests/responses that are used
throughout the API and SDK solutions, as there are shared concerns between the projects. This means any updates to
these types/classes for the API will be reflected in the SDK automatically.

### Domain

The Domain layer is essentially the core of the application, it contains all the entities that the solution is built
around. In this case, examples are FeatureFlag and AuditLog. This layer does not contain any dependencies on any other
layers, or external sources.

### Application

The Application layer contains all the business logic for the solution, and is responsible for translating between the
domain and the infrastructure/presentation layers. It also depends on the domain layer. This houses the
DTOs of the solution, custom exception implementations, interfaces, and finally services. Any validation and business
logic should be implemented here. DTO mapping in this solution utilises Riok.Mapperly.

### Infrastructure

The Infrastructure layer contains all the dependencies on external sources, such as databases, message queues, and other
services. It also depends on the application layer. This layer is responsible for implementing the
infrastructure-specific logic, such as database queries, message queue publishing, and other external service
interactions. In this case, the external services being used in this solution are a PostgreSQL database and a caching
layer using Redis. Repository implementations are also located in this layer, as well as EFCore migrations. FusionCache
is a library that provides caching for .NET applications - it can use a distributed cache like Redis, or a local
in-memory cache like MemoryCache.

### Presentation

The Presentation layer is responsible for handling the user interface and user experience of the application. In this
case, the Presentation layer is called Web.Api, which is the .NET Web API project. It contains all the controllers,
middleware, and other components that are responsible for handling HTTP requests and responses. This layer also depends
on the application layer. I went with a Minimal API approach for this solution, as it is lightweight and easy to
understand. All the Dependency Injection is handled in this layer for the entire solution. For API documentation I am
using OpenAPI and Scalar to generate a user interface for the API. The application uses JWT bearer authentication (RBAC)
for all requests(except generating a token) - as user attributes, such as claims, and their IDs need to be used to
evaluate whether a feature is enabled or not for that user and/or request.

### Contracts

Contracts is a separate project that contains common types, models, exceptions, requests/responses and has a dependency
on the Domain layer. The reasoning for this is because responses are essentially DTOs, and need to have the same fields
and properties as the domain model, an example being FeatureFlagParameters. This also allows for easy integration with
the SDK, as the SDK will use the same types and models as the API.

## Reasoning

The main reasoning behind using this architecture to design this solution is to provide myself a solid foundation of
learning and understanding the concepts of Clean Architecture. It is popular for a reason and is strong in many core
areas of software development. The layers allow for clearly defined responsibilities, which bolsters the separation of
concerns, and, in my opinion, makes the code more maintainable, testable, extensible, and readable. It may be seen as a
weakness that to add a new feature, multiple layers need to be modified and read through, but this tradeoff is
miniscule, in my opinion, compared to the benefits. Each layer can be tested in isolation, and different presentation
layers can use the same domain, infrastructure, and application layers. This allows for greater flexibility and
reusability of code and makes it easier to maintain and update the application over time. In my case, this makes
developing an SDK out of this solution much easier.

## Extras

There are some tradeoffs or patterns that I am aware of and chose not to use in this solution for various reasons.

### CQRS

Command Query Responsibility Segregation (CQRS) is a pattern that I have not used in this solution, in this scenario it
could be implemented using MediatR, but I have chosen not to do so. The reason for this is that the difference between
reads vs. writes in a feature flag service is significant, with reads being much more frequent than writes. This makes
CQRS unnecessary for this solution, as the benefits of separating reads and writes do not outweigh the complexity and
additional code required to implement it. It could be implemented in the future if the need arises, such as additional
auditing or other write-heavy operations, but for now it is not necessary.