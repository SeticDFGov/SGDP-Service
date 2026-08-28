# Rodar o SGDP + módulo PGIA localmente (antes de subir para produção)

**Totalmente independente — não precisa de Keycloak.** Guia validado nesta máquina em 28/08/2026: migrations das fases 0 a 5 aplicadas num PostgreSQL 16 real, API de pé em modo local, login de teste, órgão PGIA criado com os prazos instanciados e endpoint público anônimo respondendo.

## Pré-requisitos

| Ferramenta | Onde está |
| --- | --- |
| Docker Desktop | instalado (`docker --version`) |
| .NET 8 SDK | instalado em `%USERPROFILE%\.dotnet` (adicione ao PATH da sessão: `$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"`) |
| Node 24 + npm | instalados |

## 1. Banco (PostgreSQL em contêiner)

```bash
docker run -d --name pgia-local-pg -e POSTGRES_USER=user -e POSTGRES_PASSWORD=password -p 5432:5432 postgres:16
```

Usuário/senha/banco casam com `app/appsettings.Development.json` (`Host=localhost;Database=postgres;Username=user;Password=password`). Alternativa: `docker compose up -d postgres` na raiz do `SGDP-Service-main` (exige `.env` copiado do `.env.example` com `POSTGRES_USER=user` e `POSTGRES_PASSWORD=password`).

## 2. Migrations (cria as tabelas do SGDP + as 24 `pgia_*` com seeds)

```bash
cd app
dotnet ef database update
```

Com `ASPNETCORE_ENVIRONMENT=Development` (padrão fora de produção). **Nunca** rode este comando apontando para o banco de produção — lá quem aplica é o CI, no merge para a main.

## 3. API em MODO LOCAL (sem Keycloak)

PowerShell:

```bash
cd app
$env:Auth__ModoLocal = "true"; dotnet run
```

Sobe em `http://localhost:5148` — Swagger em `http://localhost:5148/swagger`. O CORS de desenvolvimento já libera `http://localhost:4200`.

O modo local troca só a validação de token: em vez do Keycloak, a API aceita tokens que ela mesma emite em `POST /api/authlocal/token` (e-mail + nome + perfil). Todo o resto — criação de usuário no `/me`, perfis, papéis PGIA, guards — é o código real de produção. Salvaguardas: a flag é opt-in, o endpoint devolve 404 com ela desligada, a chave de assinatura muda a cada reinício da API e **a aplicação se recusa a subir com a flag fora de `Development`**.

## 4. Front

```bash
cd ../../pgia-angular/SGDP-Angular
npm install
npx ng serve
```

Abre em `http://localhost:4200` (o `environment.ts` já aponta para `http://localhost:5148`).

## 5. Entrar com usuários de teste

Abra `http://localhost:4200/auth/local` (há um link "entrar no modo local" na tela de login quando o build não é de produção). A tela tem três blocos:

- **Papéis do PGIA** — uma persona por papel do decreto, que entra **direto na tela do papel**: Otávio (`pgia_orgao` → área do órgão), Sofia (`pgia_sgdi` → governança central), Caio (`pgia_cgtic` → comitê), Alice (agente pública **sem** papel → registrar uso/incidentes do art. 13), Aurélio (`pgia_auditoria` → auditorias designadas) e o botão **Cidadão** (página pública, sem login). O backend provisiona tudo sozinho no primeiro clique: papel, unidade e um "Órgão de Teste do PGIA" (sigla TESTE) já com os 6 prazos de adesão.
- **Perfis do SGDP** — as cinco personas originais (admin, gestor, centralit, parceiro, básico), para a regressão do sistema existente.
- **Formulário livre** — nome + e-mail + perfil + papel PGIA opcional.

Para trocar de usuário: Sair → `/auth/local` de novo. Papéis também podem ser trocados a qualquer momento pela tela de admin, como em produção (relogar com uma persona **não** apaga papel atribuído manualmente — o campo só é aplicado quando vem preenchido).

## 6. Roteiro de fumaça do PGIA

1. **Login como `admin`** (persona "Ana Admin") → `/admin`: cadastre uma Unidade; em "Governança de IA (PGIA) · Órgãos" cadastre um órgão e vincule a Unidade; em "Gerenciar Usuários", atribua papéis PGIA (`pgia_orgao`, `pgia_sgdi`, `pgia_cgtic`, `pgia_auditoria`) aos usuários de teste e ponha-os na Unidade.
2. **Papel `pgia_orgao`**: "Informações do Órgão Responsável" (designações com o prazo 02/08/2026), "Inventário" (cadastre um sistema respondendo o questionário — sem feedback de risco na tela), "Report de incidentes", "Prazos", "Capacitação", "Contratações de IA", "Relatórios e indicadores" (gere um semestral e baixe o PDF), "Solicitações do cidadão".
3. **Papel `pgia_sgdi`**: fila de homologação (questionário aberto com pesos e pontuação), aprovar/vetar Baixo/Moderado, plataformas, Registro Público, apuração de incidente, não conformidades da supervisão, painel de semestrais, Relatório Anual, auditorias (designe o usuário `pgia_auditoria`).
4. **Papel `pgia_cgtic`**: casos delegados (Alto/Excessivo) e deliberações.
5. **Papel `pgia_auditoria`**: "Auditorias designadas" → entregar parecer.
6. **Usuário sem papel** (perfil `gestor` com unidade do órgão): sidebar "Registrar uso de IA" e aviso de incidente — nada além.
7. **Sem login**: `http://localhost:4200/transparencia-ia` — Registro Público, pedido do cidadão (guarde o protocolo) e acompanhamento pelo protocolo. Responda pelo papel de órgão e consulte de novo.
8. **Regressão dos perfis atuais**: `admin`/`gestor` (dashboard e demandas), `centralit` → `/centralit`, `parceiro` → `/parceiro`, `basico` sem papel → `/pending-approval` — tudo como antes.

## Limpeza

```bash
docker rm -f pgia-local-pg
```

## Avisos

- **Nunca** ligue `Auth__ModoLocal` fora da sua máquina: em produção a aplicação nem sobe com a flag, e em qualquer ambiente compartilhado ela abriria login sem senha. Sem a flag, a API volta ao Keycloak normalmente.
- Os usuários do modo local usam e-mails `*.local.teste` — não misture com dados reais.
- O projeto `test/` legado não compila (problema conhecido). Os testes do PGIA rodam com `dotnet test test-pgia/test-pgia.csproj`.
