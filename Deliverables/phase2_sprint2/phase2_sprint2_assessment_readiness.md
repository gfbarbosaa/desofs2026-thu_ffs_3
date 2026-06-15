# Phase 2 Sprint 2 — Assessment Readiness

**Repositório:** desofs2026_thu_ffs_3  
**Turma:** thu_ffs  
**Data:** 16 de Junho de 2026

---

## Checklist de Prontidão para Avaliação

### Critério 1 — Organização e Linguagem (5%)

- [x] Repositório bem organizado com pastas `Deliverables/`, `src/`, `tests/`
- [x] Todos os deliverables linkados a partir do ficheiro principal
- [x] Documentação em língua portuguesa coerente
- [x] Sem erros gramaticais relevantes

**Referências:**

- [Deliverables/README.md](../README.md)
- [Deliverables/phase2_sprint2/phase2_sprint2_deliverable.md](phase2_sprint2_deliverable.md)

---

### Critério 2 — Desenvolvimento (35%)

- [x] **3 agregados DDD completos:** `User`, `Vault`, `Document`
- [x] **3 papéis de autorização:** `Admin`, `Manager`, `Viewer` com RBAC server-side
- [x] **Operações de SO no backend:** criação de directorias, leitura/escrita/remoção de ficheiros, logs diários
- [x] **Logging** com Serilog (consola + ficheiro diário rotativo, retenção 14 dias)
- [x] **Auditoria** de todos os eventos de segurança na BD
- [x] **IAST middleware** para detecção de padrões suspeitos em runtime
- [x] **Code review** com CODEOWNERS e template de PR com checklist de segurança
- [x] **Justificação do algoritmo:** BCrypt workfactor 12 > MD5/SHA256 para passwords (tabela comparativa no deliverable)
- [x] **Encriptação de passwords:** BCrypt — nunca armazenadas em plaintext (verificado por teste)

**Evidências:**

- [src/Domain/EntityModels/](../../src/Domain/EntityModels/) — agregados DDD
- [src/Infrastructure/Storage/FileStorageService.cs](../../src/Infrastructure/Storage/FileStorageService.cs) — operações de SO
- [src/Infrastructure/Security/PasswordHasherService.cs](../../src/Infrastructure/Security/PasswordHasherService.cs) — BCrypt
- [.github/CODEOWNERS](../../.github/CODEOWNERS) — code review obrigatório
- [tests/InfrastructureTests/SecurityInfrastructureTests.cs](../../tests/InfrastructureTests/SecurityInfrastructureTests.cs) — `PasswordHasher_UsesWorkFactor12`

---

### Critério 3 — Build e Testes (35%)

- [x] **Build automatizado** no CI em cada push/PR
- [x] **Testes unitários:** 106 testes a passar, 0 falhas
- [x] **Cobertura de código** recolhida (Cobertura XML) e publicada como artefacto
- [x] **SAST:** CodeQL com queries `security-extended,security-and-quality`
- [x] **SCA:** `dotnet list package --vulnerable --include-transitive` no CI
- [x] **DAST:** OWASP ZAP com dois modos (manual + Docker automático)
- [x] **Testes de regressão de segurança** que provam ameaças Phase 1 mitigadas
- [x] **Docker build** validado no CI (Stage 4)

**Evidências:**

- [.github/workflows/ci.yml](../../.github/workflows/ci.yml) — pipeline 4 estágios
- [.github/workflows/codeql.yml](../../.github/workflows/codeql.yml) — SAST
- [.github/workflows/dast.yml](../../.github/workflows/dast.yml) — DAST
- [tests/ApplicationTests/SecurityApplicationTests.cs](../../tests/ApplicationTests/SecurityApplicationTests.cs)
- [tests/InfrastructureTests/SecurityInfrastructureTests.cs](../../tests/InfrastructureTests/SecurityInfrastructureTests.cs)
- [tests/DomainTests/SecurityThreatMitigationTests.cs](../../tests/DomainTests/SecurityThreatMitigationTests.cs)

---

### Critério 4 — Produção (5%)

- [x] **Docker Compose** para stack completo em produção/desenvolvimento
- [x] **Non-root container** (user `safevault` no Dockerfile)
- [x] **Volumes persistentes** para dados e logs
- [x] **Health check** no serviço PostgreSQL
- [x] **Restart policy** `unless-stopped`
- [x] **Release automático** com binários multi-plataforma e imagem Docker em cada tag semântica

**Evidências:**

- [Dockerfile](../../Dockerfile)
- [docker-compose.yml](../../docker-compose.yml)
- [.github/workflows/release.yml](../../.github/workflows/release.yml)

---

### Critério 5 — Operações (5%)

- [x] **Logging estruturado** com Serilog e rotation diária
- [x] **Correlation ID** em todas as respostas de erro
- [x] **Auditoria imutável** (Viewer não pode modificar AuditLog)
- [x] **Gestão de configuração** via variáveis de ambiente
- [x] **Patch management:** SCA automático no CI detecta CVEs

---

### Critério 6 — ASVS (15%)

- [x] **ASVS checklist** actualizado com Sprint 2 (74% OK, 0% FALTA)
- [x] **Rastreabilidade** entre ameaças Phase 1, mitigações e testes
- [x] **19/22 ameaças** totalmente resolvidas e testadas (86%)

**Evidências:**

- [phase2_sprint2_asvs_checklist.md](phase2_sprint2_asvs_checklist.md)
- [phase2_sprint2_traceability_matrix.md](phase2_sprint2_traceability_matrix.md)

---

## Demonstração ao Vivo — Guia para a Avaliação

### Demo 1 — Executar os testes de segurança

```bash
dotnet test --configuration Release
# Output esperado: 106 passed, 0 failed
```

### Demo 2 — Subir o stack com Docker

```bash
cp .env.example .env
# Editar .env com POSTGRES_PASSWORD e JWT_SIGNING_KEY
docker compose up -d
# API disponível em http://localhost:8080
```

### Demo 3 — Ver o pipeline CI

Ir ao separador **Actions** do repositório GitHub e ver:

1. O workflow `ci` com os 4 estágios
2. O workflow `codeql` com resultados de SAST
3. Os artefactos gerados (coverage-report, sca-report)

### Demo 4 — Ver a justificação do bcrypt

Executar o teste `PasswordHasher_UsesWorkFactor12` e mostrar o hash gerado com `$12$`:

```bash
dotnet test --filter "PasswordHasher_UsesWorkFactor12" --configuration Release
```

### Demo 5 — Criar uma release

```bash
git tag v1.0.0
git push origin v1.0.0
# O workflow release.yml cria automaticamente:
# - Binários para Linux, Windows e macOS
# - Imagem Docker publicada no GHCR e Docker Hub
# - GitHub Release com todos os artefactos
```

### Demo 6 — Executar DAST com ZAP

```bash
# Modo manual (com API já em execução)
# No GitHub Actions: workflow_dispatch com targetUrl=http://localhost:8080
```

---

## Ameaça que Deixou de Existir (Phase 1 → Sprint 2)

**T-16 — Exposição de connection string no repositório**

Em Phase 1, esta ameaça foi identificada como crítica. O histórico do repositório mostra que em versões anteriores poderiam existir credenciais em `appsettings.Development.json`.

**Em Sprint 2:**

1. `appsettings.json` contém `"SigningKey": ""` e `"DefaultConnection": ""` — sem valores.
2. O startup do servidor lança `InvalidOperationException` se a connection string ou JWT key estiverem ausentes.
3. O CI usa GitHub Secrets para injectar estas variáveis — nunca visíveis nos logs ou no código.
4. O `.env.example` documenta as variáveis necessárias sem expor valores.

**Prova técnica:** Tentar executar a API sem definir `Jwt__SigningKey` resulta em falha imediata — não existe forma de a aplicação correr com credenciais expostas no repositório.

---

## Pendente — O que falta para completar (ZAP)

A análise DAST com OWASP ZAP contra o backend em execução está pendente de ser executada contra uma instância real. O workflow `.github/workflows/dast.yml` está pronto para ser executado:

1. **Modo Docker (automático):** sobe o stack via `docker-compose` e executa o scan automaticamente.
2. **Modo manual:** `workflow_dispatch` com URL de staging.

As regras de supressão estão em [`.zap/rules.tsv`](../../.zap/rules.tsv) para ignorar falsos positivos conhecidos (ex: ausência de autenticação no endpoint de health check).

Após executar o ZAP:

- As vulnerabilidades encontradas serão documentadas aqui.
- As que forem corrigidas serão adicionadas à matriz de rastreabilidade.
- As que forem aceites como risco residual serão documentadas com justificação.
