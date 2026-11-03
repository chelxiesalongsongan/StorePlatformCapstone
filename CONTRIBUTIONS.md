# Contributions

## Module ownership

| Module | Owner |
|---|---|
| Repository setup | Chelxie Salongsongan |
| Module 2: API design & contract-first | Chelxie Salongsongan |
| Module 3: Synchronous integration | Chelxie Salongsongan |
| Module 4: Event-driven integration | Sean Esperancilla |
| Module 5: Data & consistency | Abi Zabala (databases and migrations), Sean Esperancilla (event-driven order saga) |
| Module 6: Containerization | Abi Zabala (Dockerfiles and initial Compose), Sean Esperancilla (full-system Compose) |
| Module 8: Gateway & security | Sean Esperancilla |

## Contribution matrix

| Member | GitHub | Contributions |
|---|---|---|
| Chelxie Salongsongan | chelxiesalongsongan | Created the repository and initial project structure. **Module 2:** contract-first OpenAPI specifications for the Catalog, Inventory, Order and Payment services and the clients generated from them. **Module 3:** synchronous service integration with resilient typed clients and correlation ID propagation. Merged PRs #1 and #2. |
| Abi Zabala | abizabala | **Module 5:** database setup and EF Core migrations for the Order service (orders, items, status history) and the Catalog service. **Module 6:** Dockerfiles for the Catalog, Inventory, Order and Payment services and the first Docker Compose configuration with RabbitMQ. |
| Sean Esperancilla | sprncll | **Module 4:** shared event contracts, RabbitMQ messaging layer (publisher confirms, manual-ack consumers, dead-letter queues), idempotency guard, Inventory and Payment consumers, payment failure switch. **Module 5 (saga):** event-driven Order saga with status history and compensation, inventory seed data. **Module 6 (integration):** full-system Docker Compose with health checks and environment-based configuration, root-context Dockerfiles. **Module 8:** Auth service (JWT, admin seed), Ocelot gateway (JWT validation, roles, rate limiting, correlation ID), Storefront (login, orders, admin stock). **Docs and evidence:** Order contract update, README (architecture, run steps, messaging, eventual consistency, troubleshooting), Postman collection, evidence 1, 2, 4, 5 and 6. |

Note: Sean's commits appear under three Git names (`Sean Esperancilla`, `Seanooo`, `Sean`), all with the same email, `sprncll@gmail.com`.
