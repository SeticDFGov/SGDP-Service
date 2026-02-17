# Instruções para Correção da Migration

## Problema
A migration RefatoracaoAtividadesEtapas foi aplicada parcialmente ao banco de dados, deixando-o em estado inconsistente.

## Estado Atual do Banco
- ✅ Etapas: Colunas de datas/percentuais foram removidas
- ✅ Atividades: Colunas renomeadas (titulo → Titulo, etc.)
- ✅ Atividades: Colunas situacao e NM_PROJETO foram removidas
- ❌ Atividades: Faltam colunas novas (DT_INICIO_PREVISTO, DT_INICIO_REAL, etc.)
- ❌ Atividades: Falta coluna EtapaProjetoId e sua FK
- ❌ Projetos: Tentou adicionar DT_INICIO/DT_TERMINO que já existiam (causou erro)

## Solução - Opção 1: Script SQL Manual (RECOMENDADO)

Execute o script SQL no PostgreSQL:

```bash
cd /home/marcio/SEEC/sgpd/SGDP-Service/app
export PGPASSWORD=password
psql -h localhost -U user -d postgres -f fix_migration.sql
```

Ou conecte manualmente e execute o conteúdo de `fix_migration.sql`.

## Solução - Opção 2: Criar Nova Migration do Zero

Se preferir, podemos:
1. Fazer scaffold do banco atual para ver exatamente o que existe
2. Ajustar os models para refletir o estado atual
3. Criar migration apenas com o que falta

```bash
# 1. Ver estado atual
dotnet ef dbcontext scaffold "Host=localhost;Database=postgres;Username=user;Password=password" Npgsql.EntityFrameworkCore.PostgreSQL --output-dir TempModels --context TempDbContext --force

# 2. Comparar TempModels com app/Models/
# 3. Ajustar app/Models para match
# 4. Criar nova migration apenas com diferenças
dotnet ef migrations add CompleteRefatoracaoAtividadesEtapas
dotnet ef database update
```

## Solução - Opção 3: Executar SQL Direto Via Código

Posso criar um DbMigrator que execute o SQL de fix_migration.sql via código C#.

## Qual opção você prefere?

1. **Opção 1 (SQL manual)** - Mais rápido e seguro
2. **Opção 2 (Scaffold)** - Mais trabalhoso mas garante sincronização total
3. **Opção 3 (Migrator C#)** - Intermediário, aplica o SQL via código

## Comando para verificar estado atual:

```sql
-- Ver colunas da tabela Atividades
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'Atividades'
ORDER BY ordinal_position;

-- Ver colunas da tabela Etapas
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'Etapas'
ORDER BY ordinal_position;
```
