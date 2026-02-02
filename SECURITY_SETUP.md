# Configuração de Segurança - SGDP-Service

## Credenciais e Secrets

Este projeto foi refatorado para **não armazenar credenciais em código fonte**. Siga as instruções abaixo para configurar o ambiente de desenvolvimento e produção.

---

## Desenvolvimento Local

### Opção 1: User Secrets (Recomendado)

O .NET User Secrets armazena informações sensíveis fora do diretório do projeto.

```bash
# Navegar para o diretório do projeto
cd SGDP-Service/app

# Inicializar user secrets (já deve estar configurado)
dotnet user-secrets init

# Configurar connection string do PostgreSQL
dotnet user-secrets set "ConnectionStrings:PostgreSql" "Host=localhost;Port=5432;Database=postgres;Username=seu_usuario;Password=sua_senha"

# Configurar chave JWT (use a chave gerada abaixo)
dotnet user-secrets set "Auth:Key" "SUA_CHAVE_JWT_AQUI"

# Ver todos os secrets configurados
dotnet user-secrets list
```

### Opção 2: Arquivo .env

Para uso com Docker Compose:

```bash
# Copiar arquivo de exemplo
cp .env.example .env

# Editar .env com suas credenciais
nano .env
```

Preencha:
```env
POSTGRES_USER=seu_usuario
POSTGRES_PASSWORD=sua_senha_segura
PGADMIN_EMAIL=admin@example.com
PGADMIN_PASSWORD=senha_pgadmin

# Chave JWT (ver seção abaixo)
Auth__Key=sua_chave_jwt_256_bits
```

---

## Geração de Chave JWT

A chave JWT deve ter **no mínimo 256 bits (32 bytes)** para segurança HMAC-SHA256.

### Gerar nova chave:

```bash
# Linux/Mac
openssl rand -base64 32

# PowerShell (Windows)
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

**Exemplo de chave gerada:**
```
BpRt7BwdODeCGApRMNvPdG89jRwPgUnMXv3eS/rAFkw=
```

⚠️ **NUNCA use a chave de exemplo acima em produção!**

---

## Produção

### Variáveis de Ambiente

Em produção, configure as variáveis de ambiente diretamente no servidor/container:

```bash
# Connection String
export ConnectionStrings__PostgreSql="Host=db.exemplo.com;Port=5432;Database=sgdp_prod;Username=sgdp_user;Password=SENHA_FORTE;SSL Mode=Require"

# Chave JWT
export Auth__Key="CHAVE_JWT_SEGURA_256_BITS"

# Ambiente
export ASPNETCORE_ENVIRONMENT=Production
```

### Kubernetes Secrets

Se estiver usando Kubernetes (conforme CI/CD do projeto):

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: sgdp-secrets
type: Opaque
stringData:
  connection-string: "Host=db.exemplo.com;Port=5432;Database=sgdp_prod;Username=sgdp_user;Password=SENHA_FORTE"
  jwt-key: "CHAVE_JWT_SEGURA_256_BITS"
```

Referência no deployment:

```yaml
env:
  - name: ConnectionStrings__PostgreSql
    valueFrom:
      secretKeyRef:
        name: sgdp-secrets
        key: connection-string
  - name: Auth__Key
    valueFrom:
      secretKeyRef:
        name: sgdp-secrets
        key: jwt-key
```

---

## Validações JWT Habilitadas

Em produção, as seguintes validações JWT estão **habilitadas**:

- ✅ ValidateIssuer: true
- ✅ ValidateAudience: true
- ✅ ValidateIssuerSigningKey: true
- ✅ ValidateLifetime: true

Certifique-se de que o `Issuer` e `Audience` no `appsettings.Production.json` correspondem à URL da API em produção:

```json
{
  "Auth": {
    "Issuer": "https://subgd-api.df.gov.br/",
    "Audience": "https://subgd-api.df.gov.br/"
  }
}
```

---

## CORS Configurado

O CORS está restrito às seguintes origens em produção:

- https://subgd.df.gov.br
- https://subgd-hom.df.gov.br
- https://subgd-api.df.gov.br

Para adicionar novas origens, edite `Program.cs`:

```csharp
options.AddPolicy("CorsPolicy",
    corsBuilder => corsBuilder
        .WithOrigins(
            "https://subgd.df.gov.br",
            "https://subgd-hom.df.gov.br",
            "https://nova-origem.df.gov.br"  // Adicionar aqui
        )
        // ...
```

---

## Checklist de Segurança

Antes de fazer deploy em produção:

- [ ] Chave JWT gerada com no mínimo 256 bits
- [ ] Connection string configurada via variáveis de ambiente/secrets
- [ ] Nenhuma credencial no código fonte ou arquivos de configuração commitados
- [ ] .env adicionado ao .gitignore
- [ ] Validações JWT habilitadas em appsettings.Production.json
- [ ] CORS restrito às origens confiáveis
- [ ] SSL/TLS habilitado no PostgreSQL (modo Require)
- [ ] Certificados SSL válidos em produção

---

## Troubleshooting

### Erro: "Connection string not found"

Verifique se a variável de ambiente está configurada:
```bash
echo $ConnectionStrings__PostgreSql  # Linux/Mac
echo %ConnectionStrings__PostgreSql% # Windows CMD
$env:ConnectionStrings__PostgreSql   # PowerShell
```

### Erro: "Invalid signature" em JWT

- Verifique se a chave JWT é a mesma usada para gerar os tokens
- Confirme que `ValidateIssuerSigningKey` está true
- Certifique-se de que a chave tem no mínimo 256 bits

### CORS Error em produção

- Verifique se a origem do frontend está na lista de `WithOrigins()`
- Confirme que o protocolo (http/https) está correto
- Teste com navegador em modo incógnito (para evitar cache)

---

**Data da última atualização:** 29/01/2026
