# Phase 2 Sprint 2 — Matriz de Rastreabilidade

**Última actualização:** 16 de Junho de 2026

Este documento actualiza a matriz de rastreabilidade de Phase 1, adicionando evidências de implementação e testes de Sprint 2.
Cada ameaça identificada na análise STRIDE é mapeada para:

- A mitigação implementada (com referência de código)
- O teste que prova a mitigação (com referência de ficheiro de teste)
- O resultado actual do teste

---

## Legenda de Estado

| Estado | Significado |
|--------|-------------|
| ✅ RESOLVIDA | Mitigada e com teste de regressão a passar |
| ⚠️ PARCIAL | Mitigação arquitectural sem cobertura total de teste |
| 🔧 INFRA | Depende de configuração de infraestrutura em produção |

---

## Matriz Principal — Ameaças Phase 1 vs Sprint 2

| ID Ameaça | Ameaça | Categoria STRIDE | Requisito | Mitigação Implementada | Evidência de Código | Teste de Regressão | Estado |
|-----------|--------|-----------------|-----------|----------------------|--------------------|--------------------|--------|
| T-01 | Roubo de JWT por canal não cifrado | Spoofing | RS-03.1 | HTTPS obrigatório (`RequireHttpsMetadata=true`); HSTS header | [`Program.cs`](../../src/InterfaceAdapters/Program.cs) / [`SecurityHeadersMiddleware.cs`](../../src/InterfaceAdapters/Middleware/SecurityHeadersMiddleware.cs) | [`SecurityInfrastructureTests.JwtValidation_Rejects_TamperedPayload`](../../tests/InfrastructureTests/SecurityInfrastructureTests.cs) | ✅ RESOLVIDA |
| T-02 | Manipulação de payload JWT (algorithm confusion) | Tampering | RS-01.1 | `ValidateIssuerSigningKey=true`, `RequireSignedTokens=true`; HS256 com chave ≥ 32 chars | [`Program.cs`](../../src/InterfaceAdapters/Program.cs) / [`JwtTokenService.cs`](../../src/Infrastructure/Security/JwtTokenService.cs) | [`SecurityInfrastructureTests.JwtValidation_Rejects_AlgorithmNoneToken`](../../tests/InfrastructureTests/SecurityInfrastructureTests.cs) | ✅ RESOLVIDA |
| T-03 | Repúdio sem auditoria | Repudiation | RS-06.1 | `AuditWriterService` regista todos os eventos; logs Serilog diários | [`AuditWriterService.cs`](../../src/Infrastructure/Storage/AuditWriterService.cs) | [`AuditServiceTests`](../../tests/ApplicationTests/AuditServiceTests.cs) | ✅ RESOLVIDA |
| T-04 | Enumeração de utilizadores por resposta diferenciada | Information Disclosure | RS-04.1 | Resposta uniforme `"Invalid credentials."` para user inexistente e password errada | [`AuthService.cs`](../../src/Application/Services/AuthService.cs) | [`SecurityApplicationTests.Login_Throws_WhenPasswordIsWrong`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | ✅ RESOLVIDA |
| T-05 | Brute force / credential stuffing | Denial of Service | RS-01.4, RNF-01 | Rate limiting (10 req/min/IP) + Lockout após 5 falhas (15 min) | [`Program.cs`](../../src/InterfaceAdapters/Program.cs) / [`User.cs`](../../src/Domain/EntityModels/User.cs) | [`SecurityApplicationTests.Login_Throws_WhenAccountIsLocked`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | ✅ RESOLVIDA |
| T-06 | Comprometimento de conta Admin por brute force | Elevation of Privilege | RS-01.4 | Mesmo mecanismo de lockout de T-05; Admin não tem exceção | [`User.cs`](../../src/Domain/EntityModels/User.cs) | [`SecurityThreatMitigationTests.User_LocksOut_AfterFiveFailedAttempts`](../../tests/DomainTests/SecurityThreatMitigationTests.cs) | ✅ RESOLVIDA |
| T-07 | IDOR em documentos ou Vaults | Spoofing | RS-01.3 | `vault.CanWrite(actorId)` / `vault.CanRead(actorId)` em todas as operações | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`SecurityApplicationTests.Upload_Throws_WhenActorHasNoWriteAccess`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | ✅ RESOLVIDA |
| T-08 | Upload de ficheiro malicioso (magic bytes errados) | Tampering | RS-04.4 | Validação de magic bytes para PDF, PNG, JPEG, DOCX, TXT | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`SecurityApplicationTests.Upload_RejectsFile_WhenMagicBytesDoNotMatchMimeType`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | ✅ RESOLVIDA |
| T-09 | Path traversal via nome de ficheiro | Tampering | RS-04.3, RS-04.5 | `Path.GetFileName()` remove componentes de directório; canonicalização com `StartsWith(basePath)` | [`FileStorageService.cs`](../../src/Infrastructure/Storage/FileStorageService.cs) | [`SecurityInfrastructureTests.FileStorage_Rejects_PathTraversalInFilename`](../../tests/InfrastructureTests/SecurityInfrastructureTests.cs) | ✅ RESOLVIDA |
| T-10 | Substituição de ficheiro no filesystem | Tampering | RS-02.3 | SHA-256 calculado no upload e verificado em cada download | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`SecurityApplicationTests.Download_Throws_WhenFileHashMismatch`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | ✅ RESOLVIDA |
| T-11 | Download não autorizado | Information Disclosure | RS-01.3 | Verificação de `vault.CanRead(actorId)` antes de qualquer leitura | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`SecurityApplicationTests.Download_Throws_WhenActorHasNoReadAccess`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | ✅ RESOLVIDA |
| T-12 | Exposição de path interno em erros | Information Disclosure | RS-02.4 | `ExceptionHandlingMiddleware` retorna apenas correlation ID; sem stack trace | [`ExceptionHandlingMiddleware.cs`](../../src/InterfaceAdapters/Middleware/ExceptionHandlingMiddleware.cs) | [`ExceptionHandlingMiddlewareTests`](../../tests/InterfaceAdaptersTests/ExceptionHandlingMiddlewareTests.cs) | ✅ RESOLVIDA |
| T-13 | Upload de ficheiro grande (DoS em disco) | Denial of Service | RNF-02 | Validação de `FileSize ≤ 100MB` antes de qualquer escrita | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`DocumentServiceTests`](../../tests/ApplicationTests/DocumentServiceTests.cs) | ✅ RESOLVIDA |
| T-14 | Viewer acede a endpoints de Manager | Elevation of Privilege | RS-01.3 | `[Authorize(Roles = "Manager,Admin")]` em todos os endpoints de escrita | [`DocumentsController.cs`](../../src/InterfaceAdapters/Controllers/DocumentsController.cs) | [`ControllersCoverageTests`](../../tests/InterfaceAdaptersTests/ControllersCoverageTests.cs) | ✅ RESOLVIDA |
| T-15 | SQL Injection | Tampering | RS-04.2 | EF Core com LINQ/parametrizado; sem concatenação de strings em queries | Todos os [`Repositories/`](../../src/Infrastructure/Repositories/) | [`IastMonitoringMiddleware`](../../src/InterfaceAdapters/Middleware/IastMonitoringMiddleware.cs) detects patterns at runtime | ✅ RESOLVIDA |
| T-16 | Exposição de connection string no repositório | Information Disclosure | RS-02.2 | `appsettings.json` sem valores; injectado por variável de ambiente | [`appsettings.json`](../../src/InterfaceAdapters/appsettings.json) / [`Program.cs`](../../src/InterfaceAdapters/Program.cs) | Startup falha se connection string ausente (validação em runtime) | ✅ RESOLVIDA |
| T-17 | Manipulação de logs por Admin comprometido | Tampering | RS-06.3 | Logs em ficheiro com Serilog (write-only para app); acesso directo à BD requer credenciais de infra | [`appsettings.json`](../../src/InterfaceAdapters/appsettings.json) | Sem teste automatizado (mitigação arquitectural) | ⚠️ PARCIAL |
| T-18 | Leitura directa de ficheiros por processo não autorizado | Information Disclosure | — | Dockerfile usa non-root user `safevault`; volumes com permissões restritas | [`Dockerfile`](../../Dockerfile) | Sem teste automatizado (controlo de OS) | ⚠️ PARCIAL |
| T-19 | Esgotamento de disco por upload massivo | Denial of Service | RNF-02 | Limite de 100MB por ficheiro; rate limiting por IP | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`DocumentServiceTests`](../../tests/ApplicationTests/DocumentServiceTests.cs) | ✅ RESOLVIDA |
| T-20 | Intercepção de dados em trânsito (sem TLS) | Information Disclosure | RS-03.1 | `RequireHttpsMetadata=true`; `UseHttpsRedirection()`; HSTS header | [`Program.cs`](../../src/InterfaceAdapters/Program.cs) | TLS configurado em infra (Nginx/proxy reverso em produção) | 🔧 INFRA |
| T-21 | Modificação de ficheiro em trânsito | Tampering | RS-02.3 | TLS + SHA-256 verificado no servidor antes de entregar o ficheiro | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`SecurityApplicationTests.Download_Throws_WhenFileHashMismatch`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | ✅ RESOLVIDA |
| T-22 | Repúdio de operação de download | Repudiation | RS-06.1 | `AuditWriterService` regista todos os downloads com userId, timestamp, documentId, IP | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | [`AuditServiceTests`](../../tests/ApplicationTests/AuditServiceTests.cs) | ✅ RESOLVIDA |

---

## Sumário de Cobertura

| Estado | Nº de Ameaças | % |
|--------|--------------|---|
| ✅ RESOLVIDA | 19 | 86% |
| ⚠️ PARCIAL | 2 | 9% |
| 🔧 INFRA | 1 | 5% |
| **Total** | **22** | **100%** |

---

## Ameaça que deixou de existir entre Sprint 1 e Sprint 2

> **T-16 — Exposição de connection string no repositório** foi identificada como ameaça crítica em Phase 1.
> Em Sprint 1, o `appsettings.json` já tinha o campo vazio. Em Sprint 2, adicionamos validação em startup:
> se a connection string estiver ausente, a aplicação lança `InvalidOperationException` imediatamente,
> impedindo que qualquer versão com credenciais hardcoded passe nos testes de CI.
> Esta ameaça passou de **estado "risco activo"** para **"eliminiada por design"**.

---

## Rastreabilidade ASVS

| Requisito ASVS | Ameaça | Controlo | Teste |
|----------------|--------|---------|-------|
| V2.1.1 — Password length ≥ 12 | T-05, T-06 | `PasswordPolicy.Validate()` | `PasswordPolicy_Rejects_ShortPasswords` |
| V2.1.9 — No password complexity restrictions relaxation | T-05 | `PasswordPolicy.Validate()` | `PasswordPolicy_Rejects_WeakPasswords` |
| V2.4.1 — Password stored using adaptive hashing | T-05, T-06 | BCrypt workfactor 12 | `PasswordHasher_UsesWorkFactor12` |
| V3.2.1 — JWT tokens use secure algorithm | T-01, T-02 | HS256, RequireSignedTokens | `JwtValidation_Rejects_AlgorithmNoneToken` |
| V4.1.2 — Access control fails securely | T-07, T-11, T-14 | RBAC + ownership checks | `Upload_Throws_WhenActorHasNoWriteAccess` |
| V4.2.1 — Sensitive data not accessible by untrusted users | T-11 | `vault.CanRead()` | `Download_Throws_WhenActorHasNoReadAccess` |
| V5.2.1 — Input validated against allowlist | T-08 | Magic bytes validation | `Upload_RejectsFile_WhenMagicBytesDoNotMatchMimeType` |
| V5.3.8 — Path traversal prevented | T-09 | Canonicalização + basePath check | `FileStorage_Rejects_PathTraversalOnRead` |
| V7.1.1 — Log events include sufficient detail | T-03, T-22 | AuditWriterService | `AuditServiceTests` |
| V9.1.1 — TLS for all connections | T-20 | HTTPS redirect + HSTS | Configuração de infra |
| V12.1.1 — File size limits enforced | T-13, T-19 | 100MB limit | `DocumentServiceTests` |
