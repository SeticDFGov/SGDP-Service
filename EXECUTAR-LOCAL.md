# Rodar o SGDP + módulos PGIA e Supervisão Contínua das Contratações localmente (antes de subir para produção)

**Totalmente independente — não precisa de Keycloak.** Guia validado nesta máquina em 28/08/2026: migrations das fases 0 a 5 aplicadas num PostgreSQL 16 real, API de pé em modo local, login de teste, órgão PGIA criado com os prazos instanciados e endpoint público anônimo respondendo.

## Pré-requisitos

| Ferramenta | Onde está |
| --- | --- |
| Docker Desktop | instalado (`docker --version`) |
| .NET 8 SDK | no host (`%USERPROFILE%\.dotnet`, PATH: `$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"`) **ou** pela imagem oficial `mcr.microsoft.com/dotnet/sdk:8.0` (a mesma do `Dockerfile`) — é o caminho usado desde 2026-09-21, quando o host ficou sem SDK |
| Node 24 + npm | instalados |

## 1. Banco (PostgreSQL em contêiner)

```bash
docker run -d --name sgdp-local-pg -e POSTGRES_USER=sgdp_user -e POSTGRES_PASSWORD=sgdp_password_dev -p 5432:5432 postgres:16-alpine
```

Usuário/senha/banco casam com `app/appsettings.Development.json` (`Host=localhost;Port=5432;Database=postgres;Username=sgdp_user;Password=sgdp_password_dev`). Alternativa: `docker compose up -d postgres` na raiz do `SGDP-Service-main` (exige `.env` copiado do `.env.example` com `POSTGRES_USER=user` e `POSTGRES_PASSWORD=password`).

## 2. Migrations (cria as tabelas do SGDP, as 24 `pgia_*` com seeds e as 2 `ctr_*`)

```bash
cd SGDP-Service-main/app
dotnet ef database update
```

Sem SDK no host, pelo contêiner (Git Bash; o banco é alcançado em `host.docker.internal`):

```bash
MSYS_NO_PATHCONV=1 docker run --rm -v "E:/Trabalho/projetos/sgdp/SGDP-Service:/src" -v sgdp-nuget:/root/.nuget/packages -v sgdp-dotnet-tools:/tools -w /src/app -e ASPNETCORE_ENVIRONMENT=Development -e "ConnectionStrings__PostgreSql=Host=host.docker.internal;Port=5432;Database=postgres;Username=sgdp_user;Password=sgdp_password_dev" mcr.microsoft.com/dotnet/sdk:8.0 bash -c "([ -x /tools/dotnet-ef ] || dotnet tool install --tool-path /tools dotnet-ef --version 8.0.13) && /tools/dotnet-ef database update"
```

Com `ASPNETCORE_ENVIRONMENT=Development` (padrão fora de produção). **Nunca** rode este comando apontando para o banco de produção — lá quem aplica é o CI, no merge para a main.

## 3. API em MODO LOCAL (sem Keycloak)

PowerShell:

```bash
cd SGDP-Service-main/app
$env:Auth__ModoLocal = "true"; dotnet run
```

Sem SDK no host, pelo contêiner (a configuração `sgdp-api` do `../.claude/launch.json` faz exatamente isto):

```bash
MSYS_NO_PATHCONV=1 docker run --rm --name sgdp-api -p 5148:5148 -v "E:/Trabalho/projetos/sgdp/SGDP-Service:/src" -v sgdp-nuget:/root/.nuget/packages -w /src/app -e ASPNETCORE_ENVIRONMENT=Development -e ASPNETCORE_URLS=http://+:5148 -e Auth__ModoLocal=true -e "ConnectionStrings__PostgreSql=Host=host.docker.internal;Port=5432;Database=postgres;Username=sgdp_user;Password=sgdp_password_dev" mcr.microsoft.com/dotnet/sdk:8.0 dotnet run --no-launch-profile
```

Sobe em `http://localhost:5148` (Swagger em `http://localhost:5148/swagger`). Parar o processo do `docker run` pelo painel de preview não para o contêiner: antes de subir de novo, `docker stop sgdp-api`. O CORS de desenvolvimento já libera `http://localhost:4200`. **A chave dos tokens do modo local muda a cada reinício da API**: depois de reiniciar, entre de novo pelo `/auth/local`.

O modo local troca só a validação de token: em vez do Keycloak, a API aceita tokens que ela mesma emite em `POST /api/authlocal/token` (e-mail + nome + perfil). Todo o resto — criação de usuário no `/me`, perfis, papéis PGIA, guards — é o código real de produção. Salvaguardas: a flag é opt-in, o endpoint devolve 404 com ela desligada, a chave de assinatura muda a cada reinício da API e **a aplicação se recusa a subir com a flag fora de `Development`**.

## 4. Front

```bash
cd SGDP-Angular-main
npm install
npx ng serve
```

Abre em `http://localhost:4200` (o `environment.ts` já aponta para `http://localhost:5148`).

## 5. Entrar com usuários de teste

Abra `http://localhost:4200/auth/local` (há um link "entrar no modo local" na tela de login quando o build não é de produção). **Desde o isolamento entre módulos (2026-09-21), toda persona entra pela tela inicial `/inicio`**, com os cards dos quatro módulos: os liberados ficam ativos e os demais em cinza. A tela tem estes blocos:

- **Isolamento entre módulos**: Bruna Básico (sem acesso algum: os três módulos em cinza, sem o card da Administração, e o botão "Pedir acesso"), Débora Demandas (só Demandas, em consulta, por concessão do sistema) e Marina Multimódulo (gestor + papel de órgão no PGIA + Supervisão Contínua: três módulos).

- **Papéis do PGIA** — uma persona por papel do decreto; pelo card do PGIA cada uma abre a **trilha numerada do seu papel**: Otávio (`pgia_orgao` → área do órgão), Sofia (`pgia_sgdi` → governança central), Caio (`pgia_cgtic` → comitê), Alice (agente pública **sem** papel → registrar uso/incidentes do art. 13), Aurélio (`pgia_auditoria` → auditorias designadas) e o botão **Cidadão** (página pública, sem login). O backend provisiona tudo sozinho no primeiro clique: papel, unidade e um "Órgão de Teste do PGIA" (sigla TESTE) já com os 6 prazos de adesão.
- **Supervisão Contínua das Contratações** — persona **Clara das Contratações** (`contratacoes@local.teste`, perfil `basico` + papel `ctr_analise`), que entra direto em `/analises`: processos de contratações de TIC, painel, manifestações ao TCDF e importação da planilha. O backend provisiona o papel e a unidade central de teste no primeiro clique.
- **Perfis do SGDP**: Ana Admin (todos os módulos, inclusive Administração, com Pedidos de acesso e Gestão de acessos) e Gabriel Gestor (só Demandas, criando e editando).
- **Formulário livre** — nome + e-mail + perfil + papel PGIA opcional + papel de supervisão contínua opcional + concessões do sistema (Demandas e/ou PGIA de agente).

Para trocar de usuário: Sair → `/auth/local` de novo. Papéis também podem ser trocados a qualquer momento pela tela de admin, como em produção (relogar com uma persona **não** apaga papel atribuído manualmente — o campo só é aplicado quando vem preenchido).

## 6. Roteiro de fumaça do PGIA

1. **Login como `admin`** (persona "Ana Admin") → `/admin`: cadastre uma Unidade; em "Governança de IA (PGIA) · Órgãos" cadastre um órgão e vincule a Unidade; em "Gerenciar Usuários", atribua papéis PGIA (`pgia_orgao`, `pgia_sgdi`, `pgia_cgtic`, `pgia_auditoria`) aos usuários de teste e ponha-os na Unidade.
2. **Papel `pgia_orgao`**: "Informações do Órgão Responsável" (designações com o prazo 02/08/2026), "Inventário" (cadastre um sistema respondendo o questionário — sem feedback de risco na tela), "Report de incidentes", "Prazos", "Capacitação", "Contratações de IA", "Relatórios e indicadores" (gere um semestral e baixe o PDF), "Solicitações do cidadão".
3. **Papel `pgia_sgdi`**: fila de homologação (questionário aberto com pesos e pontuação), aprovar/vetar Baixo/Moderado, plataformas, Registro Público, apuração de incidente, não conformidades da supervisão, painel de semestrais, Relatório Anual, auditorias (designe o usuário `pgia_auditoria`).
4. **Papel `pgia_cgtic`**: casos delegados (Alto/Excessivo) e deliberações.
5. **Papel `pgia_auditoria`**: "Auditorias designadas" → entregar parecer.
6. **Usuário sem papel** (perfil `gestor` com unidade do órgão): sidebar "Registrar uso de IA" e aviso de incidente — nada além.
7. **Sem login**: `http://localhost:4200/transparencia-ia` — Registro Público, pedido do cidadão (guarde o protocolo) e acompanhamento pelo protocolo. Responda pelo papel de órgão e consulte de novo.
8. **Regressão dos perfis atuais**: `admin` vê os quatro módulos (só quem é admin vê o card da Administração); `gestor` só Demandas (painel e demandas, com criação); `basico` sem papel nem concessão vê os três módulos em cinza (a antiga tela `/pending-approval` redireciona para `/inicio`).

## 7. Roteiro de fumaça do módulo Supervisão Contínua das Contratações

1. **Login como `admin`** → `/admin`, bloco "Papel · Supervisão Contínua das Contratações" de um usuário: conceda `ctr_analise` (o perfil do SGDP não muda) e confira que o badge aparece. Remover é o mesmo select com "Sem papel".
2. **Login como a persona "Clara das Contratações"** (ou qualquer usuário com o papel) → cai em `/analises`:
   - **Importar planilha**: envie `Analises das contratações(Planilha1).csv` (o arquivo real, Windows-1252). A prévia mostra 37 linhas como "Criar"; confirme e o relatório deve trazer **37 criados, 0 rejeitados**. Confira a acentuação (ex.: "Fundação de Apoio à Pesquisa") e as **duas restituições** vindas das observações (SEMA e DER, ambas em 03/09/2026, com motivo preenchido e a observação preservada). Importar o mesmo arquivo de novo deve dar **0 criados / 37 atualizados**. **Regressão obrigatória:** antes da segunda importação, preencha criticidade e etapa do planejamento de um processo pela tela — reimportar a planilha de 12 colunas **não pode apagá-las** (coluna ausente do arquivo preserva o que está no banco; coluna presente e vazia, essa sim, limpa).
   - **Lista de processos**: busque por número/órgão/objeto, filtre por categoria, situação, sigla, restituídos, período e — desde a rodada da chefia — por **fase**, **etapa do planejamento**, **criticidade** e **origem**; use "Registrar chegada" para preencher um checkpoint com a data de hoje; exclua um processo (soft delete: some da lista) e confira que o número volta a poder ser cadastrado.
   - **Devolução dispensada**: num processo já retornado ao Gab SGDI, marque "Não se aplica" no retorno ao órgão comunicante — a situação vira **Análise concluída** (até 2026-09-22 chamava-se "Concluído") e a lista mostra "não se aplica" no lugar da data. Marcando o mesmo "não se aplica" num processo **sem** retorno ao Gab, ele continua na etapa em que estava. Informar a data desmarca o flag.
   - **Assinatura do contrato** (desde 2026-09-22): é a sexta data da seção Tramitação e a última etapa de "Registrar andamento". Preenchida, o processo fica **Concluído**, some da lista (volta com "Incluir os concluídos", em Mais filtros, ou com a situação Concluído) e entra no bloco **Processos concluídos** do painel; limpar a data reabre. Assinatura futura é recusada; anterior à chegada na SGDI é aceita (fica fora da cronologia). A etapa do planejamento (DFD/ETP/TR) continua na seção "Planejamento da contratação".
   - **Criticidade** (desde 2026-09-22): calculada pelo servidor das respostas aos sete critérios do art. 11, § 3º, da IN, na seção "Criticidade da contratação" do formulário (prévia ao vivo com os pontos; o alinhamento à EGD/DF, critério I, é o único que desconta; o critério IV tem ainda a resposta "Não foi possível avaliar com as informações apresentadas", que vale 0 ponto, como "Nenhum"); a lista vem ordenada por ela (Alta, Média, Baixa, sem criticidade). Processo classificado antes da regra mostra a caixa amarela com o valor antigo até alguém responder a um critério. Passe o mouse nas citações da IN (chips azuis) para ler o trecho exato.
   - **Dados da contratação** (desde 2026-09-22): valor estimado (R$), hospedagem no CeTIC-DF (Sim, Não, Parcialmente ou Não aplicável (SaaS)) e uso da rede GDFNet no cadastro do processo. Nenhum é obrigatório; salvar com o campo vazio o limpa, valor negativo é recusado e "Registrar andamento" não mexe neles. Reimportar a planilha de 12 colunas (ou um export antigo, de 29) não os apaga.
   - **Esclarecimentos ao órgão**: no formulário do processo, informe a data do pedido e o que foi pedido — a lista e a página passam a mostrar o badge âmbar "Aguardando esclarecimento há N dia(s)", o filtro "aguardando esclarecimento" separa os pendentes e o painel traz o card com o total. Informar a data da resposta encerra a pendência. A **situação do trâmite não muda** com isso (é sinal paralelo), mas os dias sem movimento zeram.
   - **Esclarecimentos Adicionais** (antes "Pendências identificadas pelo TCDF"): no formulário da manifestação, o campo é registro interno — aparece na lista e na página, e **não** sai no despacho em PDF. O texto gravado antes da renomeação continua lá (a coluna foi renomeada, não recriada).
   - **Regressões desta rodada**: (a) reimportar a planilha de 12 colunas **não** pode desmarcar o "não se aplica" do retorno ao órgão (célula vazia preserva; só data desmarca e só `-` marca); (b) apagar a criticidade de um processo que já tem manifestação do inciso I é recusado, e o despacho do inciso I não sai sem ela; (c) salvar o processo sem mexer na origem **não** volta um processo do TCDF para "Órgão comunicante"; (d) "Registrar chegada" funciona nas cinco etapas, inclusive no primeiro uso de um processo novo.
   - **Comunicação do TCDF** (desde 2026-09-23): em `/analises/manifestacoes/nova` o formulário pede o processo de comunicação do TCDF (máscara do SEI), a data de recebimento, o ofício com a data e o ato (Despacho Singular ou Decisão, com o número). Sem processo escolhido, informe a contratação auditada (órgão, sigla, objeto, categoria e valor) e marque o inciso II (os dias para regularização já vêm com 5): ao registrar, o processo nasce com origem **TCDF**, o número do processo de comunicação e nenhuma data de trâmite (situação "Sem movimentação"). Digitar de novo esse número numa comunicação nova mostra o aviso para registrar no processo que já existe; "Buscar o processo cadastrado" liga a comunicação a um processo já acompanhado (é o caminho do inciso I, que usa a criticidade dele). O despacho em PDF traz o processo de comunicação como "Processo SEI nº", e o estágio aparece uma vez só mesmo com status no TCDF.
   - **Exportar CSV**: o arquivo (`supervisao-continua-contratacoes.csv`) abre no Excel sem mojibake e reimporta como "Atualizar" em todas as linhas. O cabeçalho tem 32 colunas: depois das 4 da rodada da chefia (`Etapa do planejamento;Assinatura do contrato;Criticidade;Origem`) vêm as 3 do esclarecimento, as 7 dos critérios de criticidade (`Critério I - ...` a `Critério VII - ...`) e, no fim, as 3 dos dados da contratação (`Valor estimado (R$);Hospedagem no CeTIC-DF;Usa a rede GDFNet`, com o valor no formato `1234567,89`); o `-` na coluna de retorno ao órgão significa "não se aplica", como já valia na da UGTIC. Concluídos só entram no arquivo com "Incluir os concluídos" marcado.
   - **Painel**: as contagens batem com a lista filtrada (as 8 situações, com Concluído); o bloco **Concluídos (contrato assinado)** e a relação **Processos concluídos** listam os assinados; os tempos médios só contam processos com as duas datas (inclusive "chegada → assinatura"); a tabela de gargalos ignora concluídos, análise concluída e restituídos.
   - **Manifestações ao TCDF**: registre uma do **inciso I** (comunicada previamente, resultado) e uma do **inciso II** (prazo em dias); numa de "Riscos significativos" marque as duas ações e evolua o desfecho de "Aguardando resposta" para "Risco resolvido" (é edição da mesma manifestação). O inciso I é **bloqueado** enquanto o processo não tiver criticidade ("Defina a criticidade no cadastro do processo..."). Marque o **Status no TCDF** ("Suspenso por irregularidades" / "Edital revogado"): ele passa a mandar no estágio (badge e filtro) e **não** aparece no despacho. Filtre a lista por estágio e gere o **despacho em PDF** (modal Local/Nome/Cargo): as opções marcadas saem com `( X )` e as demais com `(   )`, e a criticidade impressa é a do processo.
3. **Regressão do acesso**: um `basico` **sem** o papel não vê o grupo "Supervisão Contínua das Contratações" na sidebar e é barrado ao digitar `/analises`; o módulo PGIA e os cinco perfis continuam exatamente como antes.

## 8. Roteiro de fumaça do isolamento entre módulos e da trilha do PGIA

1. **Bruna Básico** → `/inicio`: três cards em cinza (a Administração nem aparece para quem não é admin) e a faixa "Seu usuário ainda não tem acesso a nenhum módulo". Digitar `/demandas`, `/pgia` ou `/analises` na barra volta para o início com o toast. A API responde 403 em tudo (política `modulo:*`), menos nos pedidos de acesso.
2. **Pedido de acesso**: ainda como Bruna, "Pedir acesso" em Demandas abre a janela; escreva a justificativa (opcional) e envie. O card passa a "Pedido enviado em dd/mm, aguardando".
3. **Ana Admin**: o card da Administração avisa "1 pedido de acesso aguardando você" e a aba **Pedidos de acesso** tem o contador. Recuse com um motivo (obrigatório). A Bruna vê no card "Pedido recusado em dd/mm", o motivo e o botão "Pedir de novo". Em "Todos", a fila mostra o histórico (quem decidiu, quando, papel e motivo).
4. **SGDI decide o PGIA**: a Bruna pede o PGIA. **Sofia da SGDI** vê no card do PGIA "1 pedido de acesso aguardando você"; na trilha, o próximo passo recomendado vira o 1.2 ("1 pedido de acesso aguardando"). Em Pessoas e acessos, o bloco "Pedidos de acesso ao PGIA": aprove escolhendo o papel (sem papel = agente público). A Bruna vê o PGIA ativo sem novo login.
5. **Gestão de acessos** (Ana Admin): o filtro "Sem acesso a nenhum módulo" lista quem ainda não tem nada. Liberar um módulo por lá também encerra o pedido pendente da pessoa, como aprovado.
6. **Gabriel Gestor**: só Demandas (abas Painel e Demandas, sem barra lateral), com "Adicionar Demanda". O botão "Voltar ao início" fica no cabeçalho de todas as telas.
7. **Otávio do Órgão** → PGIA → visão geral com as 7 etapas numeradas, a situação de cada passo e o próximo passo recomendado (na base de teste, 1.2 "Designe o Responsável de IA", com prazo vencido em 02/08/2026). Em cada tela, a trilha fica no topo e "Próximo passo" leva adiante (1.2 → 1.3 → 2.1…). `/pgia/sistemas/lista?passo=3.1` abre a mesma lista na etapa 3 (AIA).
8. **Sofia da SGDI**: 6 etapas, a fila de homologação como passo 3.1 e, em 1.2 "Pessoas e acessos", a fila de pedidos do PGIA e a coluna "Entra no PGIA?" com o botão "Liberar como agente".
9. **Ana Admin** no PGIA: "Ver a trilha de" alterna entre as trilhas de órgão, SGDI, CGTIC, auditoria e agente.

## Limpeza

```bash
docker rm -f sgdp-local-pg sgdp-api
```

## Avisos

- **Nunca** ligue `Auth__ModoLocal` fora da sua máquina: em produção a aplicação nem sobe com a flag, e em qualquer ambiente compartilhado ela abriria login sem senha. Sem a flag, a API volta ao Keycloak normalmente.
- Os usuários do modo local usam e-mails `*.local.teste` — não misture com dados reais.
- O projeto `test/` legado não compila (problema conhecido). Os testes do PGIA rodam com `dotnet test test-pgia/test-pgia.csproj` e os do módulo Supervisão Contínua das Contratações com `dotnet test test-contratacoes/test-contratacoes.csproj`.
