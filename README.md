# Gates of Hell

Fichas de RPG para a mesa, com dois apps Blazor WebAssembly e um backend em Google Sheets
via Google Apps Script.

- **Jogadores** — criam a conta, montam a ficha, administram recursos, rolam perícias,
  consultam itens, equipamentos e habilidades e usam o rolador com a sintaxe do bot
  **Rollem** do Discord.
- **Mestre** — vê as fichas ao vivo, edita qualquer ficha, administra o inventário,
  cadastra classes, mantém o bestiário e o histórico de rolagens e configura a campanha.
- **Backend** — um único Web App do Apps Script grava tudo em uma planilha privada
  na sua conta Google. O navegador nunca acessa a planilha diretamente.

O “tempo real” é polling curto: os apps consultam o Web App a cada **5 segundos**.

## Estrutura

```
GatesOfHell.slnx
apps-script/
  Code.gs             backend completo (setup da planilha + API JSON)
  appsscript.json     manifesto V8 e configuração do Web App
  README.md           passo a passo de implantação
src/
  GatesOfHell.Shared/ modelos, motor Rollem, cliente da API, sincronização e componentes
  GatesOfHell.Players/ app dos jogadores  (http://localhost:5180)
  GatesOfHell.Master/  painel do mestre   (http://localhost:5181)
```

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) — o projeto usa `net10.0`.
- Uma conta Google para criar a planilha e o Web App.
- Nenhum banco de dados, servidor ou dependência npm é necessário.

## 1. Monte o backend no Google

O guia detalhado está em **[apps-script/README.md](apps-script/README.md)**. Resumo:

1. Abra <https://script.google.com> e crie um projeto.
2. Crie `Code.gs` e cole o conteúdo de `apps-script/Code.gs`.
3. Nas configurações do projeto, ative **Mostrar arquivo de manifesto** e cole
   `apps-script/appsscript.json`.
4. **Troque `MESTRE_CODE`** no topo de `Code.gs` por um código secreto da sua mesa.
5. Execute `setup()` uma vez e autorize. Ele cria a planilha
   **Gates of Hell — Fichas** com as abas `Config`, `Usuarios`, `Sessoes`,
   `Personagens`, `Itens`, `Classes`, `Monstros` e `Rolagens`.
6. **Implantar → Nova implantação → Aplicativo da Web**:
   - Executar como: **Eu**
   - Quem tem acesso: **Qualquer pessoa**
7. Copie a URL que termina em `/exec`.

> A planilha continua privada. “Qualquer pessoa” vale para o Web App, que valida
> login e sessão em todas as ações. Não compartilhe a planilha publicamente.

Se você editar o `Code.gs` depois, publique uma **nova versão** da implantação
(Implantar → Gerenciar implantações → editar → Nova versão).

## 2. Rode os apps

Em dois terminais, na raiz do repositório:

```bash
dotnet run --project src/GatesOfHell.Players
dotnet run --project src/GatesOfHell.Master
```

Abra <http://localhost:5180> para os jogadores e <http://localhost:5181> para o mestre.
Os perfis de execução já fixam essas portas.

Na tela de login, cole a URL `/exec` do Apps Script em **Web App**. Ela fica salva no
`localStorage` do navegador; o botão **Trocar conexão** permite informar outra.

- **Jogador:** crie uma conta livremente.
- **Mestre:** no cadastro, informe o `MESTRE_CODE` definido no Apps Script.

## 2.1. GitHub Pages

Os apps também são publicados automaticamente pelo GitHub Actions em cada
repositório:

- Jogadores: <https://thesirleaf.github.io/gates-of-hell-players/>
- Mestre: <https://thesirleaf.github.io/gates-of-hell-master/>

O workflow `.github/workflows/deploy-pages.yml` escolhe o projeto pelo nome do
repositório, publica em `wwwroot`, troca o `<base href>` para a subpasta do Pages e
cria o `404.html` para as rotas do Blazor continuarem funcionando ao recarregar.
Para atualizar, basta enviar o commit para a branch `main` do repositório desejado.

Cada pessoa pode ter mais de uma ficha. O mestre vê todas; o jogador vê apenas as
próprias fichas e os itens delas. O histórico de rolagens é compartilhado com a mesa.

## 3. Rolagens no estilo Rollem

O motor fica em `src/GatesOfHell.Shared/Dice/DiceRoller.cs` e roda no navegador; o resultado
é enviado ao backend para o histórico. A sintaxe segue o
[Rollem V1](https://rollem.rocks/docs/syntax):

| Exemplo | O que faz |
| --- | --- |
| `4d6` | quatro dados de seis lados |
| `d20+5` | um d20 com modificador |
| `4d6dl1` | quatro d6, descarta o menor |
| `4d6kh3` | quatro d6, mantém os três maiores |
| `6#4d6kh3` | repete a rolagem seis vezes |
| `10d6 << 3` | conta quantos dados saíram 3 ou menos |
| `d20 >= 15` | teste com resultado de sucesso/falha |
| `[d20+5 ataque] [2d6+3 dano]` | várias rolagens nomeadas de uma vez |
| `4d6--2` | subtrai 2 de cada dado individualmente |
| `dF` | dado Fate (`-`, `0`, `+`) |

Os limites de segurança do motor são 100 dados por pool, 1.000 lados, 20 repetições e
50 resultados por envio.

## Recursos e testes da ficha

- **Teste de atributo** = `Nd20kh1`, em que `N` é o valor do atributo, com no mínimo 1d20.
- **Vida**, **Postura** e **Stamina** são recursos ajustados manualmente; não são rolados.
- **Resistência** é o valor da armadura escolhida.

Cada perícia soma o valor dela ao teste do atributo do seu grupo: Luta com Físico 3
e Luta 2 rola `3d20kh1+2`; Percepção com Mental 2 e Percepção 1 rola `2d20kh1+1`.
As 15 perícias ficam nos grupos Físicas, Mentais e Espirituais. O mestre cadastra
classes com três perícias; a classe escolhida marca essas três e concede três slots
no orçamento, além de um slot por ponto em Mental.

## Itens, equipamentos e habilidades

O mestre cria, edita e remove itens de qualquer ficha, com tipo, descrição, valor,
quantidade e o indicador de equipado. O jogador consulta a lista na própria ficha.
O bestiário e o histórico de rolagens também são administrados pelo mestre.

## Segurança e limites

- Senhas são salgadas e guardadas com SHA-256 (três iterações). É um login básico
  para uma mesa entre amigos, não um sistema de identidade para a internet aberta.
- Sessões duram 30 dias e são guardadas na planilha apenas como hash.
- O Web App roda como o dono da planilha. As permissões são validadas no servidor:
  jogador só altera as próprias fichas; itens, classes, monstros, configurações e
  exclusão de rolagens são exclusivos do mestre.
- O Apps Script tem cotas diárias de execução. O polling de 5 s é adequado para mesas
  pequenas; para muitas pessoas, aumente `GameSyncService.Intervalo`.
- Faça backup copiando a planilha ou usando o histórico de versões do Google Sheets.

## Compilar

```bash
dotnet build GatesOfHell.slnx
```

O build gera os dois apps em `src/*/bin/`. Para publicar em um host estático, use
`dotnet publish -c Release` nos projetos `Players` e `Master` e sirva a pasta
`wwwroot` gerada, lembrando que os apps são Blazor WebAssembly puros.
