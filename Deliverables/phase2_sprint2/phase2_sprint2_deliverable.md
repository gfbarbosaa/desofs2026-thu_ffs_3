# SafeVault — Phase 2 Sprint 2 Deliverable

**Repositório:** desofs2026_thu_ffs_3  
**Turma:** thu_ffs  
**Sprint:** Phase 2 — Sprint 2  
**Data de Entrega:** 16 de Junho de 2026  

| Nome | Número |
|------|--------|
| João Loureiro | 1250526 |
| Gonçalo Barbosa | 1240454 |
| Miguel Amorim | 1250540 |
| Diogo Maria | 1201832 |

---

## Índice

1. [Objectivo da Sprint](#1-objectivo-da-sprint)
2. [Desenvolvimento (35%)](#2-desenvolvimento-35)
3. [Build e Testes (35%)](#3-build-e-testes-35)
4. [Pipeline Automation](#4-pipeline-automation)
5. [Produção e Operações (5% + 5%)](#5-produção-e-operações)
6. [ASVS (15%)](#6-asvs)
7. [Rastreabilidade — Ameaças Phase 1 → Sprint 2](#7-rastreabilidade--ameaças-phase-1--sprint-2)

---

## 1. Objectivo da Sprint

Consolidar e demonstrar a segurança do sistema SafeVault com:

- Infraestrutura Docker completa (desenvolvimento e produção).
- Pipeline CI/CD multi-estágio com SAST, SCA, testes e geração automática de releases.
- Testes de regressão de segurança que provam que as ameaças identificadas em Phase 1 foram mitigadas.
- Análise DAST automatizada via OWASP ZAP integrada no pipeline.
- Controlo de pull requests com aprovação obrigatória de membro da equipa.
- Evidência do algoritmo de hashing de passwords (bcrypt workfactor 12) como escolha justificada.
- Documentação actualizada com rastreabilidade completa entre ameaças, mitigações e testes.

---

## 2. Desenvolvimento (35%)

### 2.1 Funcionalidade — Complexidade e Boas Práticas

O backend está completamente implementado seguindo DDD e Clean Architecture:

- **3 agregados completos:** `User`, `Vault`, `Document` + agregado de suporte `AuditLog`.
- **3 papéis RBAC:** `Admin`, `Manager`, `Viewer` com verificação server-side em todos os endpoints.
- **Execução de operações no SO:**
  - Criação de directorias no filesystem ao criar vaults: [`FileStorageService.cs`](../../src/Infrastructure/Storage/FileStorageService.cs)
  - Escrita de ficheiros ao fazer upload: [`FileStorageService.cs`](../../src/Infrastructure/Storage/FileStorageService.cs)
  - Leitura de ficheiros ao fazer download: [`FileStorageService.cs`](../../src/Infrastructure/Storage/FileStorageService.cs)
  - Remoção de ficheiros ao eliminar documentos: [`FileStorageService.cs`](../../src/Infrastructure/Storage/FileStorageService.cs)
  - Geração de logs de auditoria diários: [`appsettings.json`](../../src/InterfaceAdapters/appsettings.json) (Serilog rolling file)
- **Logging e Auditoria:** Serilog com sink de ficheiro diário rotativo e sink de consola. Todos os eventos de segurança auditados na BD via `AuditWriterService`.

### 2.2 Segurança — Controlos Implementados

| Controlo | Localização | Ameaça Mitigada |
|----------|-------------|-----------------|
| BCrypt com work factor 12 | [`PasswordHasherService.cs`](../../src/Infrastructure/Security/PasswordHasherService.cs) | T-05, T-06, RS-01.5 |
| JWT HS256 com chave ≥ 32 chars | [`JwtTokenService.cs`](../../src/Infrastructure/Security/JwtTokenService.cs) | T-01, T-02 |
| RBAC server-side por controller | [`DocumentsController.cs`](../../src/InterfaceAdapters/Controllers/DocumentsController.cs) | T-07, T-11, T-14 |
| Validação de magic bytes (upload) | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | T-08, AC-04 |
| Path traversal prevention (canonicalização) | [`FileStorageService.cs`](../../src/Infrastructure/Storage/FileStorageService.cs) | T-09, AC-03 |
| Hash SHA-256 em upload + verificação no download | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | T-10, T-21 |
| Rate limiting 10 req/min por IP no auth | [`Program.cs`](../../src/InterfaceAdapters/Program.cs) | T-05, AC-01 |
| Lockout de conta após 5 falhas | [`User.cs`](../../src/Domain/EntityModels/User.cs) | T-05, T-06 |
| Secrets fora do repositório (env vars) | [`appsettings.json`](../../src/InterfaceAdapters/appsettings.json) | T-16 |
| CSRF token dinâmico (30 min) | [`CsrfTokenService.cs`](../../src/Infrastructure/Security/CsrfTokenService.cs) | Mutations |
| Headers de segurança HTTP | [`SecurityHeadersMiddleware.cs`](../../src/InterfaceAdapters/Middleware/SecurityHeadersMiddleware.cs) | T-20 |
| Erros sem detalhe interno + correlation ID | [`ExceptionHandlingMiddleware.cs`](../../src/InterfaceAdapters/Middleware/ExceptionHandlingMiddleware.cs) | T-12 |
| IAST middleware (runtime pattern detection) | [`IastMonitoringMiddleware.cs`](../../src/InterfaceAdapters/Middleware/IastMonitoringMiddleware.cs) | T-15, AC-07 |
| EF Core com queries parametrizadas | Todos os repositórios em [`Repositories/`](../../src/Infrastructure/Repositories/) | T-15, AC-07 |
| Validação de tamanho de ficheiro (max 100MB) | [`DocumentService.cs`](../../src/Application/Services/DocumentService.cs) | T-13, T-19 |

### 2.3 Justificação do Algoritmo de Password Hashing

O sistema utiliza **BCrypt** com work factor 12, implementado em [`PasswordHasherService.cs`](../../src/Infrastructure/Security/PasswordHasherService.cs).

**Por que BCrypt e não MD5, SHA-1 ou SHA-256 directo?**

| Algoritmo | Velocidade | Salt Automático | Adaptive Cost | Adequado para Passwords |
|-----------|-----------|----------------|---------------|------------------------|
| MD5 | ~10 GB/s (GPU) | Não | Não | **NÃO** |
| SHA-256 | ~5 GB/s (GPU) | Não | Não | **NÃO** |
| SHA-256 + salt manual | ~4 GB/s (GPU) | Manual | Não | **NÃO** |
| BCrypt (cost=12) | ~150 H/s (GPU) | Sim (built-in) | Sim | **SIM** |
| Argon2id | ~50 H/s (GPU) | Sim (built-in) | Sim | **SIM** (melhor alternativa) |

BCrypt é **7 ordens de magnitude mais lento** que MD5 para um atacante com GPU, tornando ataques de força bruta offline computacionalmente inviáveis. O work factor 12 produz ~300ms por hash no servidor, o que é aceitável para autenticação mas impraticável para ataques em massa.

O teste [`SecurityInfrastructureTests.PasswordHasher_UsesWorkFactor12`](../../tests/InfrastructureTests/SecurityInfrastructureTests.cs) verifica em código que o work factor 12 está efectivamente a ser usado (string `$12$` no hash gerado).

### 2.4 Controlo de Pull Requests com Aprovação Obrigatória

- **CODEOWNERS** configurado para exigir revisão de `@mei-desofs` em todos os ficheiros:
  - [`.github/CODEOWNERS`](../../.github/CODEOWNERS)
- **Template de PR** com checklist de segurança obrigatório:
  - [`.github/pull_request_template.md`](../../.github/pull_request_template.md)
- **Branch protection** (a configurar em Settings → Branches → main):
  - ≥ 1 aprovação obrigatória
  - "Require review from Code Owners" activado
  - Dismiss stale reviews activado
  - Todos os checks de CI obrigatórios antes de merge

> **Nota:** A configuração de branch protection é feita na interface do GitHub (Settings > Branches). Não pode ser versionada em ficheiros, mas está documentada no CODEOWNERS e no template de PR.

---

## 3. Build e Testes (35%)

### 3.1 Pipeline CI Multi-Estágio

O pipeline CI foi reestruturado em 4 estágios sequenciais/paralelos:

```
Stage 1: Build  →  Stage 2: Test & Coverage
                →  Stage 3: SCA (paralelo com Stage 2)
                           ↓
                    Stage 4: Docker Build (só após Stage 2 + 3 passarem)
```

Ficheiro: [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)

**Estágios:**

| Estágio | O que faz | Artefacto produzido |
|---------|-----------|---------------------|
| Stage 1 — Build | `dotnet build --configuration Release` | Cache da compilação |
| Stage 2 — Test & Coverage | `dotnet test` com Cobertura XML | `coverage-report/` + resultados TRX |
| Stage 3 — SCA | `dotnet list package --vulnerable --include-transitive` | `sca-report.txt` |
| Stage 4 — Docker Build | `docker build` (sem push) para validar Dockerfile | — |

### 3.2 SAST — CodeQL

CodeQL configurado com queries estendidas de segurança (`security-extended,security-and-quality`):

- [`.github/workflows/codeql.yml`](../../.github/workflows/codeql.yml)
- Executa em cada push/PR para `main` e semanalmente (cron).
- Resultados publicados no separador "Security" do repositório GitHub.

### 3.3 DAST — OWASP ZAP

Dois modos de execução:

**Modo A — Manual (externo):** `workflow_dispatch` com URL de staging como parâmetro.

**Modo B — Automático (Docker):** Sobe o stack completo (API + PostgreSQL via docker-compose) e executa o scan contra `http://localhost:8080`. Regras de supressão configuradas em [`.zap/rules.tsv`](../../.zap/rules.tsv).

Ficheiro: [`.github/workflows/dast.yml`](../../.github/workflows/dast.yml)

Relatório ZAP publicado como artefacto (HTML + JSON) em cada execução.

### 3.4 Testes de Regressão de Segurança

Foram criados testes especificamente para demonstrar que as ameaças identificadas em Phase 1 estão mitigadas. Cada teste está anotado com o ID da ameaça correspondente.

**Ficheiros criados:**

| Ficheiro | Ameaças cobertas |
|----------|-----------------|
| [`SecurityThreatMitigationTests.cs`](../../tests/DomainTests/SecurityThreatMitigationTests.cs) | T-05, T-06, AC-01, RS-01.4, RS-01.5 |
| [`SecurityApplicationTests.cs`](../../tests/ApplicationTests/SecurityApplicationTests.cs) | AC-02, AC-04, T-07, T-08, T-10, T-11, RS-01.3, RS-02.3, RS-04.4 |
| [`SecurityInfrastructureTests.cs`](../../tests/InfrastructureTests/SecurityInfrastructureTests.cs) | T-01, T-02, T-09, RS-01.5, RS-04.5 |

**Cobertura de segurança por teste:**

| Teste | Ameaça Mitigada | Resultado |
|-------|----------------|-----------|
| `User_LocksOut_AfterFiveFailedAttempts` | T-05, T-06 (brute force) | ✅ PASS |
| `PasswordPolicy_Rejects_ShortPasswords` | RS-01.5 (password fraca) | ✅ PASS |
| `PasswordPolicy_Rejects_WeakPasswords` | RS-01.5 (sem complexidade) | ✅ PASS |
| `Upload_RejectsFile_WhenMagicBytesDoNotMatchMimeType` | T-08, AC-04 (upload malicioso) | ✅ PASS |
| `Upload_Throws_WhenActorHasNoWriteAccess` | T-14, AC-02 (IDOR write) | ✅ PASS |
| `Download_Throws_WhenActorHasNoReadAccess` | T-11, AC-02 (IDOR read) | ✅ PASS |
| `Download_Throws_WhenFileHashMismatch` | T-10, T-21 (integridade) | ✅ PASS |
| `Login_Throws_WhenAccountIsLocked` | T-05, AC-01 (brute force) | ✅ PASS |
| `JwtValidation_Rejects_AlgorithmNoneToken` | T-01, T-02 (JWT tampering) | ✅ PASS |
| `JwtValidation_Rejects_TamperedPayload` | T-02 (payload elevation) | ✅ PASS |
| `FileStorage_Rejects_PathTraversalInFilename` | T-09, AC-03 (path traversal) | ✅ PASS |
| `FileStorage_Rejects_PathTraversalOnRead` | T-09, AC-03 (path traversal) | ✅ PASS |
| `PasswordHasher_DoesNotStore_PlaintextPassword` | RS-01.5 (bcrypt) | ✅ PASS |
| `PasswordHasher_UsesWorkFactor12` | RS-01.5 (cost factor) | ✅ PASS |
| `PasswordHasher_ProducesDifferentHashesForSameInput` | RS-01.5 (salt único) | ✅ PASS |

**Total de testes (todo o projecto):** 106 — 0 falhas.

### 3.5 Análise de Dependências (SCA)

O Stage 3 do pipeline executa `dotnet list package --vulnerable --include-transitive` e gera um relatório. Evidência de execução disponível como artefacto `sca-report` em cada run de CI.

A análise confirmou que os pacotes actuais **não têm vulnerabilidades CVE conhecidas** na data de entrega.

---

## 4. Pipeline Automation

### 4.1 Workflows Implementados

| Workflow | Trigger | O que faz |
|----------|---------|-----------|
| [`ci.yml`](../../.github/workflows/ci.yml) | Push/PR para `main` | Build → Test → SCA → Docker Build |
| [`codeql.yml`](../../.github/workflows/codeql.yml) | Push/PR `main`, cron semanal | SAST com CodeQL (security-extended) |
| [`dast.yml`](../../.github/workflows/dast.yml) | Manual ou workflow_call | DAST com OWASP ZAP |
| [`release.yml`](../../.github/workflows/release.yml) | Push de tag `v*.*.*` | Testes → Binários multi-plataforma → Docker → GitHub Release |

### 4.2 Pipeline de Release Automático

Ao fazer push de uma tag semântica (ex: `v1.0.0`), o workflow `release.yml` executa:

1. **Testes completos** — nenhuma release é criada se os testes falharem.
2. **Build de binários self-contained** para 3 plataformas:
   - `linux-x64` → `.tar.gz`
   - `windows-x64` → `.zip` (executável `.exe` incluído)
   - `macos-x64` → `.tar.gz`
3. **Imagem Docker multi-arch** (linux/amd64, linux/arm64) publicada em:
   - GitHub Container Registry (GHCR): `ghcr.io/<owner>/safevault-api:<version>`
   - Docker Hub: `<dockerhub_user>/safevault-api:<version>`
4. **GitHub Release** criado automaticamente com todos os artefactos e release notes geradas automaticamente.

**Variáveis secretas necessárias no repositório:**

- `DOCKERHUB_USERNAME` — utilizador do Docker Hub
- `DOCKERHUB_TOKEN` — token de acesso ao Docker Hub

### 4.3 Docker — Execução Local

```bash
# 1. Copiar e preencher variáveis de ambiente
cp .env.example .env
# editar .env: POSTGRES_PASSWORD e JWT_SIGNING_KEY

# 2. Subir o stack completo (API + PostgreSQL)
docker compose up -d

# 3. API disponível em http://localhost:8080
# Swagger UI disponível em http://localhost:8080/swagger (apenas em Development)
```

Ficheiros Docker:

- [`Dockerfile`](../../Dockerfile) — build multi-stage, execução como non-root user
- [`docker-compose.yml`](../../docker-compose.yml) — stack local completo
- [`docker-compose.dast.yml`](../../docker-compose.dast.yml) — override para DAST com ZAP
- [`.dockerignore`](../../.dockerignore) — exclui build artifacts e secrets
- [`.env.example`](../../.env.example) — template para variáveis de ambiente

---

## 5. Produção e Operações

### 5.1 Gestão de Infraestrutura de Produção

- **Docker Compose** como orquestrador local/produção mínima com volumes persistentes para dados e logs.
- **Non-root container:** O Dockerfile cria um utilizador dedicado `safevault` para executar a aplicação.
- **Health check** no serviço PostgreSQL garante que a API só inicia quando a BD está pronta.
- **Restart policy** `unless-stopped` garante recuperação automática após crash.

### 5.2 Logging e Rastreabilidade

- **Serilog** com dois sinks: consola (desenvolvimento) e ficheiro diário rotativo (`logs/audit-YYYYMMDD.log`).
- **Retenção:** 14 dias de logs.
- **Correlation ID** em todas as respostas de erro para rastreabilidade de incidentes.
- **Auditoria na BD:** todos os eventos de segurança (login, upload, download, delete) registados em `AuditLog`.

### 5.3 Gestão de Configuração e Segredos

- Sem credenciais versionadas no repositório.
- Secrets injectados via variáveis de ambiente (Docker env, GitHub Secrets).
- Validação em runtime: startup falha imediatamente se JWT key ou connection string estiverem ausentes.
- Ficheiro `.env.example` documenta as variáveis necessárias sem expor valores reais.

---

## 6. ASVS

O ASVS checklist actualizado está em:

- [`phase2_sprint2_asvs_checklist.md`](phase2_sprint2_asvs_checklist.md)

---

## 7. Rastreabilidade — Ameaças Phase 1 → Sprint 2

A matrix de rastreabilidade actualizada com evidências de código e testes está em:

- [`phase2_sprint2_traceability_matrix.md`](phase2_sprint2_traceability_matrix.md)

**Resumo:** Das 22 ameaças identificadas em Phase 1, **19 estão totalmente mitigadas e testadas**, 2 são parcialmente mitigadas (T-17 manipulação de logs de auditoria por Admin — mitigação arquitectural sem solução técnica completa, e T-18 permissões de filesystem), e 1 (T-20 TLS) depende de configuração de infraestrutura em produção.

---

## 8. Referências

- [OWASP Top 10 2021](https://owasp.org/Top10/)
- [OWASP ASVS v4.0](https://github.com/OWASP/ASVS)
- [OWASP ZAP](https://www.zaproxy.org/)
- [NIST SP 800-63B — Password Guidelines](https://pages.nist.gov/800-63-3/sp800-63b.html)
- [BCrypt vs SHA for passwords](https://security.stackexchange.com/a/31846)
- [GitHub Actions CodeQL](https://docs.github.com/en/code-security/code-scanning)
