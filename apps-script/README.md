# Backend no Google Apps Script

Este diretório contém o backend completo do Gates of Hell. Ele cria e mantém uma
planilha Google e expõe uma API JSON usada pelos dois apps Blazor.

## O que o `setup()` faz

- Cria a planilha **Gates of Hell — Fichas** (ou reaproveita a que já estiver
  registrada nas propriedades do script).
- Cria as abas `Config`, `Usuarios`, `Sessoes`, `Personagens`, `Itens`, `Monstros`
  e `Rolagens`.
- Escreve os cabeçalhos, congela a primeira linha, aplica o tema escuro/dourado e
  define larguras de coluna.
- Grava `nomeCampanha = Gates of Hell` na aba `Config`.
- Guarda o ID da planilha em `ScriptProperties` com a chave
  `GATES_OF_HELL_SPREADSHEET_ID`.

É seguro executar `setup()` de novo: ele não recria a planilha nem duplica abas.
Para começar do zero, crie um projeto novo (ou apague a propriedade
`GATES_OF_HELL_SPREADSHEET_ID` nas configurações do projeto e execute de novo).

## Passo a passo

1. Abra <https://script.google.com> e crie um **Novo projeto**.
2. Renomeie o projeto para `Gates of Hell`.
3. No editor, substitua o conteúdo de `Code.gs` pelo arquivo
   [`Code.gs`](Code.gs) deste diretório.
4. Abra **Configurações do projeto** (ícone de engrenagem) e marque
   **Mostrar arquivo de manifesto “appsscript.json”**.
5. Substitua o conteúdo de `appsscript.json` pelo arquivo
   [`appsscript.json`](appsscript.json) deste diretório.
6. Em `Code.gs`, troque a constante no topo:

   ```js
   const MESTRE_CODE = 'GATES-MESTRE';
   ```

   Use um código longo e só seu. Ele é exigido no cadastro de mestre.

7. Selecione a função `setup` na barra de funções e clique em **Executar**.
   Autorize o acesso à sua conta Google quando pedido.
8. Confira o registro de execução: ele mostra a URL da planilha criada.
9. **Implantar → Nova implantação → Aplicativo da Web**:
   - **Descrição:** `Gates of Hell API`
   - **Executar como:** `Eu`
   - **Quem tem acesso:** `Qualquer pessoa`
10. Autorize novamente se for pedido e copie a URL da implantação, que termina
    em `/exec`.

Cole essa URL no campo **Web App** da tela de login dos apps.

### Atualizações

Ao alterar o código, a URL `/exec` continua a mesma, mas é preciso publicar uma nova
versão:

**Implantar → Gerenciar implantações → (lápis) → Versão: Nova versão → Implantar**.

## Estrutura da planilha

| Aba | Conteúdo |
| --- | --- |
| `Config` | `nomeCampanha` |
| `Usuarios` | id, nome, usuário, hash da senha, salt, papel, criado em |
| `Sessoes` | hash do token, usuário, criado em, expira em (30 dias) |
| `Personagens` | ficha completa, dono e timestamps |
| `Itens` | itens, equipamentos e habilidades por ficha |
| `Monstros` | bestiário do mestre |
| `Rolagens` | uma linha por resultado, histórico compartilhado |

A planilha é a fonte da verdade. Datas ficam em ISO 8601 (UTC); números e booleanos
são gravados como números e booleanos. Textos iniciados por `=`, `+`, `-` ou `@`
recebem o prefixo de texto do Sheets para não virarem fórmulas.

## API

Todas as chamadas são `POST` com `Content-Type: text/plain` e um JSON no corpo.
Esse formato evita o preflight de CORS, que o Apps Script não responde.
A resposta é sempre um envelope:

```json
{ "ok": true, "data": { } }
{ "ok": false, "error": "mensagem para o usuário" }
```

Ações: `ping`, `login`, `register`, `me`, `logout`, `sync`, `createCharacter`,
`saveCharacter`, `deleteCharacter`, `saveItem`, `deleteItem`, `saveMonster`,
`deleteMonster`, `saveRoll`, `deleteRoll`, `clearRolls`, `saveConfig`.

Ações autenticadas enviam `token`. O servidor valida o dono da ficha, o papel de
mestre e a sessão em cada chamada. `sync` devolve ao jogador apenas as fichas e itens
dele, as rolagens compartilhadas e a configuração; o mestre recebe também monstros,
todas as fichas e a lista pública de usuários (sem hash nem salt).

`ping` pode ser chamado antes do `setup()` e informa se a planilha já foi configurada.
Abrir a URL `/exec` no navegador mostra uma página de status; `?action=ping` devolve
o JSON.

## Limites e cuidados

- **Cotas:** o Apps Script limita execuções e tempo por conta. Os apps sincronizam a
  cada 5 segundos; para uma mesa pequena isso é confortável, mas não é um sistema de
  tempo real de baixa latência.
- **CORS:** use sempre `POST` com corpo `text/plain`. Cabeçalhos personalizados
  disparam um preflight `OPTIONS` que o Apps Script não trata.
- **Acesso:** a implantação precisa estar como **Qualquer pessoa**, senão o Google
  devolve uma página de login e os apps não conseguem ler a resposta.
- **Privacidade:** a planilha fica somente na sua conta. Não a torne pública.
- **Login básico:** as senhas usam sal + SHA-256, adequado para uma mesa de amigos.
  Para uso público de verdade, troque por um provedor de identidade.
- **Backup:** use o histórico de versões do Google Sheets ou copie a planilha
  periodicamente. Apagar uma aba quebra o backend até rodar `setup()` de novo.
