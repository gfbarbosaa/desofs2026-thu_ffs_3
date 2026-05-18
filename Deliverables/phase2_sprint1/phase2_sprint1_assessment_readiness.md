# Phase 2 Sprint 1 - Assessment Readiness

Este documento mapeia a rubrica 6.2 aos artefactos e evidências implementadas nesta sprint.

---

## 1. Cobertura da rubrica 6.2

| Critério | Peso | Estado | Evidência principal |
|---|---|---|---|
| Organization and Language | 5% | OK | [phase2_sprint1_deliverable.md](phase2_sprint1_deliverable.md), [README.md](README.md) |
| Development | 30% | OK | [src/Application/Services/DocumentService.cs](../../src/Application/Services/DocumentService.cs), [src/InterfaceAdapters/Middleware/CsrfTokenMiddleware.cs](../../src/InterfaceAdapters/Middleware/CsrfTokenMiddleware.cs), [src/InterfaceAdapters/Middleware/ExceptionHandlingMiddleware.cs](../../src/InterfaceAdapters/Middleware/ExceptionHandlingMiddleware.cs), [src/InterfaceAdapters/Middleware/IastMonitoringMiddleware.cs](../../src/InterfaceAdapters/Middleware/IastMonitoringMiddleware.cs) |
| Build and Test | 30% | OK | [\.github/workflows/ci.yml](../../.github/workflows/ci.yml), [tests](../../tests) |
| Pipeline automation | 20% | OK | [\.github/workflows/ci.yml](../../.github/workflows/ci.yml), [\.github/workflows/codeql.yml](../../.github/workflows/codeql.yml), [\.github/workflows/dast.yml](../../.github/workflows/dast.yml) |
| ASVS | 15% | OK | [phase2_sprint1_asvs_checklist.md](phase2_sprint1_asvs_checklist.md) |

---

## 2. Evidências por tópico

| Tópico | Evidência |
|---|---|
| SAST | [\.github/workflows/codeql.yml](../../.github/workflows/codeql.yml) |
| SCA | [\.github/workflows/ci.yml](../../.github/workflows/ci.yml) |
| DAST | [\.github/workflows/dast.yml](../../.github/workflows/dast.yml) |
| IAST | [src/InterfaceAdapters/Middleware/IastMonitoringMiddleware.cs](../../src/InterfaceAdapters/Middleware/IastMonitoringMiddleware.cs) |
| CSRF dinâmico | [src/Infrastructure/Security/CsrfTokenService.cs](../../src/Infrastructure/Security/CsrfTokenService.cs), [src/InterfaceAdapters/Middleware/CsrfTokenMiddleware.cs](../../src/InterfaceAdapters/Middleware/CsrfTokenMiddleware.cs) |
| Magic bytes | [src/Application/Services/DocumentService.cs](../../src/Application/Services/DocumentService.cs) |
| Error handling seguro | [src/InterfaceAdapters/Middleware/ExceptionHandlingMiddleware.cs](../../src/InterfaceAdapters/Middleware/ExceptionHandlingMiddleware.cs) |
| Secrets removidos | [src/InterfaceAdapters/appsettings.json](../../src/InterfaceAdapters/appsettings.json), [src/InterfaceAdapters/appsettings.Development.json](../../src/InterfaceAdapters/appsettings.Development.json) |

---

## 3. Inventário de deliverables

- Documento principal: [phase2_sprint1_deliverable.md](phase2_sprint1_deliverable.md)
- ASVS Sprint 1: [phase2_sprint1_asvs_checklist.md](phase2_sprint1_asvs_checklist.md)
- Matriz de rastreabilidade: [phase2_sprint1_traceability_matrix.md](phase2_sprint1_traceability_matrix.md)
