![logoGDF](https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRPl5qRPe9__KUYRMGc9KGf0DgexWv7hrPqVg&s)

# Controle de demandas

Seviço desenvolvido e aprimorado para o controle de demandas interna dentro da subsecretaria de transformação digital e central de relacionamento do GDF

### Como instalar


#### Windows e MacOs

##### Modo 1

- Abra um navegador da web e acesse o site oficial da Microsoft .NET: https://dotnet.microsoft.com/download/dotnet/8.0
- Role a página até a seção ".NET 8 SDK" e clique no botão de download adequado para seu sistema operacional (por exemplo, "macOS x64 Installer" para macOS 64 bits ou  "Windows x64 Installer" para Windows 64 bits).
- O arquivo de instalação será baixado. Depois que o download for concluído, clique duas vezes no arquivo para iniciá-lo.
- O instalador será aberto. Leia e aceite os termos de licença.
- Selecione as opções de instalação que você deseja.
- Clique no botão "Install" (Instalar) para iniciar a instalação do .NET 8.
- Após a conclusão da instalação, você verá uma tela informando que o .NET 8 SDK foi instalado com sucesso.
- Para verificar se a instalação foi bem-sucedida, abra o Prompt de Comando ou o PowerShell e execute o seguinte comando:

```bash
dotnet --version
```
- Isso exibirá a versão do .NET instalada, confirmando se o .NET 8 está configurado corretamente.

##### Modo 2

Basta instalar a IDE [Visual Studio](https://visualstudio.microsoft.com/pt-br/free-developer-offers/) escolhendo a versão gratuita (Versão Community). Após instalar o Visual Studio, ele automaticamente irá instalar o .NET com a versão mais estável.

#### Linux

Instale o SDK do *.*NET .

```bash
sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-8.0
```

Instale o runtime ASP.NET Core.

```bash
sudo apt-get update && \
  sudo apt-get install -y aspnetcore-runtime-8.0
```

Entre na pasta do serviço. Dentro da pasta "app" rode o comando:

```bash
dotnet run
```
### Clonar Aplicação

Para clonar o repostório, basta utilizar o comando abaixo:

##### SGDP-Service
```
git clone https://github.com/SeticDFGov/SGDP-Service.git
```

### Como Rodar
### Utilizando docker-compose

#### Pré-requisitos
- Docker
- Docker-compose

#### Windows 
Rode o seguinte comando na pasta da aplicação.
```bash
docker-compose build && docker-compose up
```


#### Linux ou MacOS
Rode o seguinte comando na pasta da aplicação.
```bash
sudo docker-compose build && sudo docker-compose up
```

#### Usando Visual Studio Code

Para rodar utilizando o VS Code, basta seguir a seguinte instrução:

Entre na pasta do serviço. Dentro da pasta "app" rode o comando:

```bash
dotnet run
```

### Encerrando a aplicação

- No terminal em que a aplicação esta rodando, digite simultaneamente as teclas **ctrl**+**c**. 
- Caso esteja utilizando o Visual Studio, clique no ícone quadrado vermelho <br>.


### Documentação endpoints

Para documentar os endpoints estamos utilizando o Swagger. Caso queira visualizar, basta abrir a rota: 
```bash
http://localhost:5148/swagger/index.html
```

### Licença

O projeto SGDP-Service está sob as regras aplicadas na licença [MIT](https://github.com/SeticDFGov/SGDP-Service/blob/main/LICENSE)
