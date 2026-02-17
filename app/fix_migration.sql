-- Script para corrigir o estado do banco após migration parcialmente aplicada
-- Execute este script manualmente no PostgreSQL

BEGIN;

-- Verificar estado atual das colunas em Atividades
DO $$
BEGIN
    -- Se a migration parou parcialmente, algumas colunas já foram renomeadas
    -- Vamos completar o que faltou

    -- Adicionar colunas que faltam em Atividades (se não existirem)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name='Atividades' AND column_name='DT_INICIO_PREVISTO') THEN
        ALTER TABLE "Atividades" ADD COLUMN "DT_INICIO_PREVISTO" timestamp with time zone NOT NULL DEFAULT TIMESTAMP '2000-01-01';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name='Atividades' AND column_name='DT_INICIO_REAL') THEN
        ALTER TABLE "Atividades" ADD COLUMN "DT_INICIO_REAL" timestamp with time zone NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name='Atividades' AND column_name='DT_TERMINO_REAL') THEN
        ALTER TABLE "Atividades" ADD COLUMN "DT_TERMINO_REAL" timestamp with time zone NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name='Atividades' AND column_name='RESPONSAVEL_ATIVIDADE') THEN
        ALTER TABLE "Atividades" ADD COLUMN "RESPONSAVEL_ATIVIDADE" character varying(200) NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name='Atividades' AND column_name='Order') THEN
        ALTER TABLE "Atividades" ADD COLUMN "Order" integer NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name='Atividades' AND column_name='EtapaProjetoId') THEN
        ALTER TABLE "Atividades" ADD COLUMN "EtapaProjetoId" integer NOT NULL DEFAULT 1;
    END IF;

    -- Alterar tipos de colunas existentes
    ALTER TABLE "Atividades" ALTER COLUMN "Titulo" TYPE character varying(200);
    ALTER TABLE "Atividades" ALTER COLUMN "Descricao" TYPE character varying(500);
    ALTER TABLE "Atividades" ALTER COLUMN "Descricao" DROP NOT NULL;
    ALTER TABLE "Atividades" ALTER COLUMN "Categoria" TYPE character varying(100);
    ALTER TABLE "Atividades" ALTER COLUMN "Categoria" DROP NOT NULL;

    -- Alterar Etapas para permitir NULL
    ALTER TABLE "Etapas" ALTER COLUMN "RESPONSAVEL_ETAPA" DROP NOT NULL;
    ALTER TABLE "Etapas" ALTER COLUMN "ANALISE" DROP NOT NULL;

END$$;

-- Criar índice e FK se não existir
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Atividades_EtapaProjetoId') THEN
        CREATE INDEX "IX_Atividades_EtapaProjetoId" ON "Atividades" ("EtapaProjetoId");
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Atividades_Etapas_EtapaProjetoId') THEN
        ALTER TABLE "Atividades"
        ADD CONSTRAINT "FK_Atividades_Etapas_EtapaProjetoId"
        FOREIGN KEY ("EtapaProjetoId")
        REFERENCES "Etapas" ("EtapaProjetoId")
        ON DELETE CASCADE;
    END IF;
END$$;

-- Registrar a migration como aplicada (se ainda não estiver)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260203141946_RefatoracaoAtividadesEtapas', '8.0.13'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260203141946_RefatoracaoAtividadesEtapas'
);

COMMIT;

-- Verificar o resultado
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'Atividades'
ORDER BY ordinal_position;
