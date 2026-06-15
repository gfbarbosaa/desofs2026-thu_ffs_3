# SafeVault — Guia de Setup numa Máquina Nova

Este guia assume que partes do zero: sem Docker, sem .NET, sem PostgreSQL instalados.

---

## Pré-requisitos — O que instalar

### 1. Git

```
https://git-scm.com/download/win
```

Instala e reinicia o terminal.

### 2. Docker Desktop

```
https://www.docker.com/products/docker-desktop/
```

- Instala Docker Desktop para Windows.
- Durante a instalação, escolhe **WSL 2** como backend (recomendado).
- Após instalar, abre o Docker Desktop e espera até o ícone ficar verde ("Engine running").
- Verifica na linha de comandos:

```cmd
docker --version
docker compose version
```

### 3. .NET SDK 9 (só para desenvolvimento local — não precisas para correr com Docker)

```
https://dotnet.microsoft.com/download/dotnet/9.0
```

```cmd
dotnet --version
```

Deve mostrar `9.0.x`.

---

## Passo 1 — Clonar o repositório

```cmd
git clone https://github.com/<teu-org>/desofs2026-thu_ffs_3.git
cd desofs2026-thu_ffs_3
```

---

## Passo 2 — Configurar variáveis de ambiente

**Copia o ficheiro de exemplo:**

```cmd
copy .env.example .env
```

**Edita o `.env`** com um editor de texto (Notepad, VS Code, etc.):

```
POSTGRES_PASSWORD=UmaPasswordForte!2026
JWT_SIGNING_KEY=UmaChaveJwtSuperSecretaComMaisde32Caracteres!2026
```

> ⚠️ **NUNCA faças commit do ficheiro `.env`** — está no `.gitignore`.  
> A `JWT_SIGNING_KEY` tem de ter **no mínimo 32 caracteres**.

---

## Passo 3 — Construir e arrancar o stack Docker

```cmd
docker compose up -d --build
```

O que este comando faz:

1. **Constrói a imagem Docker** da API (demora 1-3 min na primeira vez).
2. **Arranca a base de dados PostgreSQL** no container `db`.
3. **Arranca a API** no container `api`, aguardando que a BD esteja pronta.
4. `-d` corre em background (detached).

---

## Passo 4 — Verificar que tudo está a funcionar

### Verificar os containers

```cmd
docker compose ps
```

Deves ver algo como:

```
NAME              IMAGE                SERVICE   STATUS
safevault-db-1    postgres:16-alpine   db        running (healthy)
safevault-api-1   safevault-api        api       running (healthy)
```

Ambos devem mostrar **running (healthy)**.

### Testar o health check da API

```cmd
curl http://localhost:8080/health
```

Resposta esperada:

```
Healthy
```

Se não tens `curl`, abre o browser em: **<http://localhost:8080/health>**

### Verificar os logs da API

```cmd
docker compose logs api
```

Deves ver linhas como:

```
[INF] Now listening on: http://[::]:8080
[INF] Application started.
```

### Verificar os logs da base de dados

```cmd
docker compose logs db
```

Deves ver:

```
database system is ready to accept connections
```

---

## Passo 5 — Testar a API com o Swagger

> ⚠️ O Swagger só está disponível em modo `Development`. Em modo `Docker` (produção), está desactivado por segurança.

Para activar o Swagger temporariamente em modo Docker, muda temporariamente no `docker-compose.yml`:

```yaml
ASPNETCORE_ENVIRONMENT: Development
```

Depois: `docker compose up -d`

Acede a: **<http://localhost:8080/swagger>**

---

## Passo 6 — Aplicar as migrações da base de dados

A API usa Entity Framework Core com PostgreSQL. As migrações são aplicadas automaticamente no arranque.

Se precisares de aplicar manualmente:

```cmd
docker compose exec api dotnet ef database update
```

Ou usando o script SQL directamente:

```cmd
docker compose exec db psql -U safevault -d safevault -f /app/ef_initial_migration.sql
```

---

## Passo 7 — Testar endpoints básicos

### Registar um utilizador Admin

```cmd
curl -X POST http://localhost:8080/api/auth/register ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"admin@safevault.pt\",\"password\":\"Admin@SafeVault2026!\",\"role\":0}"
```

(`role: 0` = Admin, `1` = Manager, `2` = Viewer)

### Fazer login e obter JWT

```cmd
curl -X POST http://localhost:8080/api/auth/login ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"admin@safevault.pt\",\"password\":\"Admin@SafeVault2026!\"}"
```

Resposta:

```json
{
  "accessToken": "eyJhbGci...",
  "expiresAtUtc": "2026-06-15T15:00:00Z",
  "refreshToken": "...",
  "refreshExpiresAtUtc": "..."
}
```

### Usar o token para criar um Vault

```cmd
curl -X POST http://localhost:8080/api/vaults ^
  -H "Authorization: Bearer <access_token>" ^
  -H "Content-Type: application/json" ^
  -d "{\"name\":\"RH\",\"description\":\"Documentos de RH\"}"
```

---

## Passo 8 — Ver os logs de auditoria

Os logs são gravados em dois locais:

**1. Na base de dados** (via API):

```cmd
curl -X GET http://localhost:8080/api/audit ^
  -H "Authorization: Bearer <admin_token>"
```

**2. Em ficheiro** (rolling daily log):

```cmd
docker compose exec api ls /app/logs/
docker compose exec api cat /app/logs/audit-20260615.log
```

---

## Parar o stack

```cmd
docker compose down
```

Para parar **e apagar os volumes** (base de dados e storage — IRREVERSÍVEL):

```cmd
docker compose down -v
```

---

## Correr os testes unitários localmente

```cmd
dotnet test --configuration Release
```

Resultado esperado: `106 passed, 0 failed`

---

## Correr o scan de segurança localmente (opcional)

### SCA — verificar pacotes vulneráveis

```cmd
dotnet list package --vulnerable --include-transitive
```

### Secret scan com Gitleaks (se tiveres instalado)

```
https://github.com/gitleaks/gitleaks/releases
```

```cmd
gitleaks detect --config .gitleaks.toml --verbose
```

---

## Visão geral dos GitHub Actions Workflows

Quando fazes `git push`, o GitHub corre automaticamente:

| Workflow | Quando corre | O que faz |
|---------|-------------|-----------|
| `ci.yml` | Cada push/PR para `main` | **Stage 1:** Compila → **Stage 2:** Corre os 106 testes + coverage → **Stage 3:** SCA (vulnerabilidades NuGet) → **Stage 4:** Valida build Docker |
| `codeql.yml` | Push/PR `main` + semanal | **SAST:** Analisa o código C# à procura de vulnerabilidades (SQL injection, XSS, etc.) |
| `secret-scan.yml` | Cada push/PR | **Secret scan:** Gitleaks detecta credenciais hardcoded + verifica `appsettings.json` tem campos vazios |
| `dast.yml` | Manual ou chamado | **DAST:** OWASP ZAP corre contra a API e reporta vulnerabilidades |
| `release.yml` | Push de tag `v1.0.0` | Cria release: binários Windows/Linux/macOS + Docker push + GitHub Release |

### O que é cada tipo de análise

- **SAST** (Static Application Security Testing) = analisa o **código fonte** sem executar a aplicação. O CodeQL lê os ficheiros `.cs` e detecta padrões inseguros.
- **SCA** (Software Composition Analysis) = verifica se as **dependências NuGet** têm CVEs (vulnerabilidades conhecidas publicamente).
- **DAST** (Dynamic Application Security Testing) = executa a **aplicação a correr** e testa os endpoints com ataques reais (ZAP faz SQL injection, XSS, etc. automaticamente).
- **IAST** (Interactive Application Security Testing) = middleware dentro da própria aplicação que monitoriza pedidos em runtime e detecta padrões suspeitos.
- **Secret Scanning** = verifica se há credenciais, chaves ou passwords hardcoded no código.

---

## Configurar branch protection no GitHub (aprovação obrigatória em PRs)

1. Vai ao repositório no GitHub.
2. Settings → Branches → "Add branch ruleset" (ou "Add rule").
3. Branch name pattern: `main`
4. Activa:
   - ✅ **Require a pull request before merging**
   - ✅ **Required approving reviews: 1**
   - ✅ **Require review from Code Owners**
   - ✅ **Dismiss stale pull request approvals when new commits are pushed**
   - ✅ **Require status checks to pass before merging**
   - Adicionar como status checks obrigatórios: `Stage 1 · Build`, `Stage 2 · Test & Coverage`, `Stage 3 · SCA`, `Secret Scan · Gitleaks`
5. Save.

---

## Criar uma release (versão nova da app)

```cmd
git tag v1.0.0
git push origin v1.0.0
```

O GitHub Actions faz o resto automaticamente:

1. Corre todos os testes.
2. Compila binários para Windows (`.exe`), Linux e macOS.
3. Publica a imagem Docker no GHCR e Docker Hub.
4. Cria uma GitHub Release com todos os artefactos.

---

## Troubleshooting

### API não arranca — "JWT signing key must be set"

→ O `.env` não está configurado ou o `JWT_SIGNING_KEY` tem menos de 32 caracteres.

### API não arranca — "DefaultConnection must be set"

→ O `.env` não tem `POSTGRES_PASSWORD` definido.

### Container `db` não fica healthy

→ Espera 30-60 segundos. Se persistir: `docker compose logs db`

### Porta 8080 ocupada

→ Muda a porta no `docker-compose.yml`: `"8081:8080"` e acede em `localhost:8081`.

### Ver o conteúdo da base de dados

```cmd
docker compose exec db psql -U safevault -d safevault
```

```sql
\dt          -- listar tabelas
SELECT * FROM "Users";
SELECT * FROM "AuditLogs" ORDER BY "CreatedAtUtc" DESC LIMIT 10;
\q           -- sair
```
