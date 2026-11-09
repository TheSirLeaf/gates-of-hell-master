/**
 * Gates of Hell — backend de fichas em Google Sheets.
 *
 * COMO USAR
 * 1. Crie um projeto em https://script.google.com e cole este arquivo.
 * 2. Troque MESTRE_CODE abaixo por um código secreto da sua mesa.
 * 3. Execute setup() uma vez e autorize o acesso.
 * 4. Implante > Nova implantação > Aplicativo da Web.
 *    Executar como: eu. Quem tem acesso: qualquer pessoa.
 * 5. Copie a URL /exec e informe nos dois apps Blazor.
 *
 * A planilha fica privada na sua conta. Os apps falam apenas com este Web App.
 */

const MESTRE_CODE = 'GATES-MESTRE';
const NOME_PLANILHA = 'Gates of Hell — Fichas';
const PROP_PLANILHA = 'GATES_OF_HELL_SPREADSHEET_ID';
const DIAS_SESSAO = 30;
const LIMITE_ROLAGENS = 80;
const MAX_RESULTADOS_ROLAGEM = 50;

const ABAS = {
  CONFIG: 'Config',
  USUARIOS: 'Usuarios',
  SESSOES: 'Sessoes',
  PERSONAGENS: 'Personagens',
  ITENS: 'Itens',
  MONSTROS: 'Monstros',
  ROLAGENS: 'Rolagens'
};

const CABECALHOS = {
  Config: ['chave', 'valor'],
  Usuarios: ['id', 'nome', 'usuario', 'senhaHash', 'salt', 'papel', 'criadoEm'],
  Sessoes: ['tokenHash', 'usuarioId', 'criadoEm', 'expiraEm'],
  Personagens: [
    'id', 'usuarioId', 'nome', 'classe', 'raca', 'periciaClasse', 'armadura', 'classeValor',
    'vida', 'postura', 'stamina', 'resistencia', 'fisico', 'mental', 'espiritual',
    'luta', 'iniciativa', 'esquiva', 'fortitude', 'atletismo',
    'percepcao', 'historia', 'linguagem', 'pontaria', 'alquimia',
    'vontade', 'furtividade', 'instinto', 'intimidacao', 'enganacao',
    'criadoEm', 'atualizadoEm'
  ],
  Itens: [
    'id', 'personagemId', 'tipo', 'nome', 'descricao', 'valor', 'quantidade',
    'equipado', 'criadoEm', 'atualizadoEm'
  ],
  Monstros: [
    'id', 'nome', 'vida', 'vidaMax', 'postura', 'stamina', 'resistencia',
    'fisico', 'mental', 'espiritual', 'notas', 'criadoEm', 'atualizadoEm'
  ],
  Rolagens: [
    'id', 'criadoEm', 'usuarioId', 'autor', 'contexto', 'personagemId',
    'expressao', 'rotulo', 'resultado', 'detalhe', 'sucesso'
  ]
};

const TIPOS_ITEM = ['Item', 'Equipamento', 'Habilidade'];

/* ------------------------------------------------------------------ */
/* Entrada                                                            */
/* ------------------------------------------------------------------ */

function doPost(e) {
  try {
    const corpo = lerCorpo_(e);
    const acao = texto_(corpo.action);
    return resposta_({ ok: true, data: despachar_(acao, corpo) });
  } catch (erro) {
    if (erro instanceof ErroApi) {
      return resposta_({ ok: false, error: erro.message });
    }
    console.error('Erro inesperado no Gates of Hell', erro && erro.stack ? erro.stack : erro);
    return resposta_({
      ok: false,
      error: 'Erro interno no Web App. Confira os registros do Apps Script.'
    });
  }
}

function doGet(e) {
  const acao = texto_(e && e.parameter ? e.parameter.action : '');
  if (acao === 'ping') {
    return resposta_({ ok: true, data: dadosPing_() });
  }

  const html =
    '<!doctype html><html lang="pt-BR"><meta charset="utf-8">' +
    '<meta name="viewport" content="width=device-width,initial-scale=1">' +
    '<title>Gates of Hell — API</title>' +
    '<style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#0b0908;' +
    'color:#e9ded3;font-family:Georgia,serif;text-align:center;padding:24px}' +
    'h1{color:#d3ad68;font-weight:500;letter-spacing:.04em}' +
    'p{color:#a99b8e;font-family:system-ui,sans-serif;line-height:1.6;max-width:34rem}' +
    'code{color:#e2542c;font-family:ui-monospace,monospace}</style>' +
    '<main><h1>Gates of Hell</h1>' +
    '<p>O Web App está no ar. Ele responde às requisições dos apps de fichas; ' +
    'abra o app dos jogadores ou o painel do mestre para usar a mesa.</p>' +
    '<p><code>setup()</code> já foi executado? Então basta informar esta URL <code>/exec</code> no login.</p>' +
    '</main></html>';

  return HtmlService.createHtmlOutput(html).setTitle('Gates of Hell — API');
}

function despachar_(acao, corpo) {
  switch (acao) {
    case 'ping':
      return dadosPing_();
    case 'login':
      return login_(corpo);
    case 'register':
      return registrar_(corpo);
    case 'me':
      return usuarioSaida_(exigirUsuario_(corpo));
    case 'logout':
      return logout_(corpo);
    case 'sync':
      return sincronizar_(corpo);
    case 'createCharacter':
      return criarPersonagem_(corpo);
    case 'saveCharacter':
      return salvarPersonagem_(corpo);
    case 'deleteCharacter':
      return excluirPersonagem_(corpo);
    case 'saveItem':
      return salvarItem_(corpo);
    case 'deleteItem':
      return excluirItem_(corpo);
    case 'saveMonster':
      return salvarMonstro_(corpo);
    case 'deleteMonster':
      return excluirMonstro_(corpo);
    case 'saveRoll':
      return salvarRolagem_(corpo);
    case 'deleteRoll':
      return excluirRolagem_(corpo);
    case 'clearRolls':
      return limparRolagens_(corpo);
    case 'saveConfig':
      return salvarConfiguracao_(corpo);
    default:
      throw new ErroApi('Ação desconhecida: ' + (acao || '(vazia)'));
  }
}

class ErroApi extends Error {
  constructor(mensagem) {
    super(mensagem);
    this.name = 'ErroApi';
  }
}

function lerCorpo_(e) {
  const bruto = e && e.postData && e.postData.contents ? e.postData.contents : '';
  if (!bruto) {
    throw new ErroApi('Requisição vazia. Atualize o app e tente novamente.');
  }
  try {
    const corpo = JSON.parse(bruto);
    if (!corpo || typeof corpo !== 'object' || Array.isArray(corpo)) {
      throw new Error('corpo inválido');
    }
    return corpo;
  } catch (erro) {
    throw new ErroApi('Requisição inválida: o corpo precisa ser um JSON de objeto.');
  }
}

function resposta_(payload) {
  return ContentService
    .createTextOutput(JSON.stringify(payload))
    .setMimeType(ContentService.MimeType.JSON);
}

/* ------------------------------------------------------------------ */
/* Instalação                                                         */
/* ------------------------------------------------------------------ */

/**
 * Cria (ou reaproveita) a planilha, as abas e os cabeçalhos.
 * Execute esta função manualmente uma vez, no editor do Apps Script.
 */
function setup() {
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) {
    throw new Error('Não foi possível obter o bloqueio da planilha. Tente novamente.');
  }

  try {
    const props = PropertiesService.getScriptProperties();
    let planilha = null;
    const idSalvo = props.getProperty(PROP_PLANILHA);

    if (idSalvo) {
      try {
        planilha = SpreadsheetApp.openById(idSalvo);
      } catch (erro) {
        console.warn('A planilha salva não está acessível; criando outra.', erro);
      }
    }

    if (!planilha) {
      planilha = SpreadsheetApp.create(NOME_PLANILHA);
      props.setProperty(PROP_PLANILHA, planilha.getId());
    }

    garantirEstrutura_(planilha);

    const url = planilha.getUrl();
    console.log('Planilha pronta: ' + url);
    console.log('Agora implante como Aplicativo da Web e use a URL /exec nos apps.');
    return url;
  } finally {
    lock.releaseLock();
  }
}

function garantirEstrutura_(planilha) {
  const nomes = Object.keys(CABECALHOS);

  let config = planilha.getSheetByName(ABAS.CONFIG);
  if (!config) {
    const vazia = planilha.getSheets().filter(function (s) {
      return s.getLastRow() === 0 && nomes.indexOf(s.getName()) === -1;
    })[0];
    if (vazia) {
      vazia.setName(ABAS.CONFIG);
      config = vazia;
    } else {
      config = planilha.insertSheet(ABAS.CONFIG);
    }
  }

  nomes.forEach(function (nome) {
    let aba = planilha.getSheetByName(nome);
    if (!aba) aba = planilha.insertSheet(nome);

    const headers = CABECALHOS[nome];
    aba.getRange(1, 1, 1, headers.length)
      .setValues([headers])
      .setFontWeight('bold')
      .setBackground('#1c1512')
      .setFontColor('#d3ad68')
      .setVerticalAlignment('middle');

    aba.setFrozenRows(1);
    aba.setColumnWidths(1, headers.length, nome === ABAS.CONFIG ? 200 : 150);

    if (nome !== ABAS.CONFIG && aba.getLastRow() > 1) {
      aba.getRange(2, 1, aba.getLastRow() - 1, headers.length)
        .setVerticalAlignment('top')
        .setWrap(false);
    }
  });

  const padroes = {
    nomeCampanha: 'Gates of Hell'
  };

  const linhas = config.getLastRow() > 1
    ? config.getRange(2, 1, config.getLastRow() - 1, 2).getValues()
    : [];
  const existentes = {};
  linhas.forEach(function (linha) {
    existentes[texto_(linha[0])] = true;
  });

  Object.keys(padroes).forEach(function (chave) {
    if (!existentes[chave]) {
      config.appendRow([chave, padroes[chave]]);
    }
  });

  config.setFrozenRows(1);
  config.getRange(1, 1, 1, 2)
    .setValues([CABECALHOS.Config])
    .setFontWeight('bold')
    .setBackground('#1c1512')
    .setFontColor('#d3ad68');
  config.setColumnWidth(1, 220);
  config.setColumnWidth(2, 260);

  planilha.setSpreadsheetTimeZone(Session.getScriptTimeZone());
}

/* ------------------------------------------------------------------ */
/* Autenticação                                                       */
/* ------------------------------------------------------------------ */

function login_(corpo) {
  const login = normalizarLogin_(corpo.usuario);
  const senha = texto_(corpo.senha);

  if (!login || !senha) {
    throw new ErroApi('Informe usuário e senha.');
  }

  const usuario = lerTabela_(ABAS.USUARIOS).filter(function (u) {
    return texto_(u.usuario) === login;
  })[0];

  if (!usuario || hashSenha_(senha, texto_(usuario.salt)) !== texto_(usuario.senhaHash)) {
    throw new ErroApi('Usuário ou senha incorretos.');
  }

  const token = comLock_(function () {
    return criarSessao_(texto_(usuario.id));
  });

  return { token: token, usuario: usuarioSaida_(usuario) };
}

function registrar_(corpo) {
  const nome = limitarTexto_(corpo.nome, 60);
  const login = normalizarLogin_(corpo.usuario);
  const senha = texto_(corpo.senha);
  const codigo = texto_(corpo.codigoMestre);

  if (nome.length < 2) {
    throw new ErroApi('Informe um nome com pelo menos 2 caracteres.');
  }
  if (!/^[a-z0-9._-]{3,24}$/.test(login)) {
    throw new ErroApi('O usuário deve ter de 3 a 24 caracteres, usando apenas letras sem acento, números, ponto, _ ou -.');
  }
  if (senha.length < 6) {
    throw new ErroApi('A senha precisa ter pelo menos 6 caracteres.');
  }
  if (senha.length > 200) {
    throw new ErroApi('A senha é longa demais.');
  }
  if (codigo && codigo !== MESTRE_CODE) {
    throw new ErroApi('Código de mestre inválido.');
  }

  const papel = codigo === MESTRE_CODE && codigo ? 'mestre' : 'jogador';

  return comLock_(function () {
    const usuarios = lerTabela_(ABAS.USUARIOS);
    const duplicado = usuarios.some(function (u) {
      return texto_(u.usuario) === login;
    });
    if (duplicado) {
      throw new ErroApi('Este usuário já está cadastrado.');
    }

    const agora = agora_();
    const id = Utilities.getUuid();
    const salt = Utilities.getUuid();
    const usuario = {
      id: id,
      nome: nome,
      usuario: login,
      senhaHash: hashSenha_(senha, salt),
      salt: salt,
      papel: papel,
      criadoEm: agora
    };

    adicionarLinhas_(ABAS.USUARIOS, [usuario]);
    const token = criarSessao_(id);
    return { token: token, usuario: usuarioSaida_(usuario) };
  });
}

function logout_(corpo) {
  const token = texto_(corpo.token);
  if (token) {
    const hash = hashToken_(token);
    const sessao = lerTabela_(ABAS.SESSOES).filter(function (s) {
      return texto_(s.tokenHash) === hash;
    })[0];
    if (sessao) {
      excluirPorId_(ABAS.SESSOES, 'tokenHash', hash);
    }
  }
  return { ok: true };
}

function exigirUsuario_(corpo) {
  const token = texto_(corpo.token);
  if (!token || token.length > 200) {
    throw new ErroApi('Sua sessão expirou. Entre novamente.');
  }

  const hash = hashToken_(token);
  const sessao = lerTabela_(ABAS.SESSOES).filter(function (s) {
    return texto_(s.tokenHash) === hash;
  })[0];

  if (!sessao) {
    throw new ErroApi('Sua sessão expirou. Entre novamente.');
  }

  if (new Date(texto_(sessao.expiraEm)).getTime() < Date.now()) {
    excluirPorId_(ABAS.SESSOES, 'tokenHash', hash);
    throw new ErroApi('Sua sessão expirou. Entre novamente.');
  }

  const usuario = lerTabela_(ABAS.USUARIOS).filter(function (u) {
    return texto_(u.id) === texto_(sessao.usuarioId);
  })[0];

  if (!usuario) {
    excluirPorId_(ABAS.SESSOES, 'tokenHash', hash);
    throw new ErroApi('Sua sessão expirou. Entre novamente.');
  }

  return usuario;
}

function exigirMestre_(corpo) {
  const usuario = exigirUsuario_(corpo);
  if (texto_(usuario.papel) !== 'mestre') {
    throw new ErroApi('Somente o mestre pode fazer isso.');
  }
  return usuario;
}

function criarSessao_(usuarioId) {
  limparSessoesExpiradas_();

  const token = (Utilities.getUuid() + Utilities.getUuid()).replace(/-/g, '') +
    (Utilities.getUuid() + Utilities.getUuid()).replace(/-/g, '');
  const agora = new Date();
  const expira = new Date(agora.getTime() + DIAS_SESSAO * 24 * 60 * 60 * 1000);

  adicionarLinhas_(ABAS.SESSOES, [{
    tokenHash: hashToken_(token),
    usuarioId: usuarioId,
    criadoEm: agora.toISOString(),
    expiraEm: expira.toISOString()
  }]);

  return token;
}

function limparSessoesExpiradas_() {
  const agora = Date.now();
  const sessoes = lerTabela_(ABAS.SESSOES);
  const expiradas = sessoes.filter(function (s) {
    return new Date(texto_(s.expiraEm)).getTime() < agora;
  });

  if (!expiradas.length) return;

  const aba = aba_(ABAS.SESSOES);
  for (let i = sessoes.length - 1; i >= 0; i--) {
    if (new Date(texto_(sessoes[i].expiraEm)).getTime() < agora) {
      aba.deleteRow(i + 2);
    }
  }
}

function hashSenha_(senha, salt) {
  let valor = texto_(senha) + '|' + texto_(salt);
  for (let i = 0; i < 3; i++) {
    valor = bytesParaHex_(Utilities.computeDigest(
      Utilities.DigestAlgorithm.SHA_256,
      valor,
      Utilities.Charset.UTF_8
    ));
  }
  return valor;
}

function hashToken_(token) {
  return bytesParaHex_(Utilities.computeDigest(
    Utilities.DigestAlgorithm.SHA_256,
    texto_(token),
    Utilities.Charset.UTF_8
  ));
}

function bytesParaHex_(bytes) {
  return bytes.map(function (b) {
    const n = (b + 256) % 256;
    return (n < 16 ? '0' : '') + n.toString(16);
  }).join('');
}

function normalizarLogin_(valor) {
  return texto_(valor).toLowerCase();
}

/* ------------------------------------------------------------------ */
/* Sincronização                                                      */
/* ------------------------------------------------------------------ */

function sincronizar_(corpo) {
  const usuario = exigirUsuario_(corpo);
  const mestre = texto_(usuario.papel) === 'mestre';

  const todosPersonagens = lerTabela_(ABAS.PERSONAGENS).map(personagemSaida_);
  const personagens = mestre
    ? todosPersonagens
    : todosPersonagens.filter(function (p) { return p.usuarioId === texto_(usuario.id); });

  const ids = {};
  personagens.forEach(function (p) { ids[p.id] = true; });

  const itens = lerTabela_(ABAS.ITENS)
    .map(itemSaida_)
    .filter(function (i) { return mestre || ids[i.personagemId]; });

  const rolagens = lerTabela_(ABAS.ROLAGENS)
    .slice(-LIMITE_ROLAGENS)
    .reverse()
    .map(rolagemSaida_);

  const monstros = mestre ? lerTabela_(ABAS.MONSTROS).map(monstroSaida_) : [];
  const usuarios = mestre ? lerTabela_(ABAS.USUARIOS).map(usuarioSaida_) : [];

  return {
    personagens: personagens,
    itens: itens,
    monstros: monstros,
    rolagens: rolagens,
    usuarios: usuarios,
    config: lerConfiguracao_(),
    serverTime: agora_()
  };
}

/* ------------------------------------------------------------------ */
/* Personagens                                                        */
/* ------------------------------------------------------------------ */

function criarPersonagem_(corpo) {
  const usuario = exigirUsuario_(corpo);
  const mestre = texto_(usuario.papel) === 'mestre';
  const dono = mestre && texto_(corpo.usuarioId) ? texto_(corpo.usuarioId) : texto_(usuario.id);

  if (mestre) {
    const existe = lerTabela_(ABAS.USUARIOS).some(function (u) {
      return texto_(u.id) === dono;
    });
    if (!existe) throw new ErroApi('Jogador não encontrado.');
  }

  const agora = agora_();
  const personagem = {
    id: Utilities.getUuid(),
    usuarioId: dono,
    nome: limitarTexto_(corpo.nome, 80) || 'Sem nome',
    classe: limitarTexto_(corpo.classe, 60),
    raca: limitarTexto_(corpo.raca, 60),
    periciaClasse: '',
    armadura: '',
    classeValor: 0,
    vida: 0,
    postura: 0,
    stamina: 0,
    resistencia: 0,
    fisico: 0,
    mental: 0,
    espiritual: 0,
    luta: 0,
    iniciativa: 0,
    esquiva: 0,
    fortitude: 0,
    atletismo: 0,
    percepcao: 0,
    historia: 0,
    linguagem: 0,
    pontaria: 0,
    alquimia: 0,
    vontade: 0,
    furtividade: 0,
    instinto: 0,
    intimidacao: 0,
    enganacao: 0,
    criadoEm: agora,
    atualizadoEm: agora
  };

  comLock_(function () {
    adicionarLinhas_(ABAS.PERSONAGENS, [personagem]);
  });

  return personagemSaida_(personagem);
}

function salvarPersonagem_(corpo) {
  const usuario = exigirUsuario_(corpo);
  const dados = corpo.personagem;
  if (!dados || typeof dados !== 'object') {
    throw new ErroApi('Ficha inválida.');
  }

  const id = texto_(dados.id);
  if (!id) throw new ErroApi('Ficha sem identificador.');

  return comLock_(function () {
    const atuais = lerTabela_(ABAS.PERSONAGENS);
    const indice = atuais.findIndex(function (p) { return texto_(p.id) === id; });
    if (indice < 0) throw new ErroApi('Ficha não encontrada.');

    const atual = personagemSaida_(atuais[indice]);
    const mestre = texto_(usuario.papel) === 'mestre';
    if (!mestre && atual.usuarioId !== texto_(usuario.id)) {
      throw new ErroApi('Você só pode editar as próprias fichas.');
    }

    const nome = limitarTexto_(dados.nome, 80);
    if (!nome) throw new ErroApi('Dê um nome ao personagem.');

    const salvo = {
      id: atual.id,
      usuarioId: atual.usuarioId,
      nome: nome,
      classe: limitarTexto_(dados.classe, 60),
      raca: limitarTexto_(dados.raca, 60),
      periciaClasse: limitarTexto_(dados.periciaClasse, 60),
      armadura: limitarTexto_(dados.armadura, 80),
      classeValor: numeroInteiro_(dados.classeValor, 0, 999, 0),
      vida: numeroInteiro_(dados.vida, 0, 9999, 0),
      postura: numeroInteiro_(dados.postura, 0, 9999, 0),
      stamina: numeroInteiro_(dados.stamina, 0, 9999, 0),
      resistencia: numeroInteiro_(dados.resistencia, 0, 999, 0),
      fisico: numeroInteiro_(dados.fisico, 0, 99, 0),
      mental: numeroInteiro_(dados.mental, 0, 99, 0),
      espiritual: numeroInteiro_(dados.espiritual, 0, 99, 0),
      luta: numeroInteiro_(dados.luta, 0, 99, 0),
      iniciativa: numeroInteiro_(dados.iniciativa, 0, 99, 0),
      esquiva: numeroInteiro_(dados.esquiva, 0, 99, 0),
      fortitude: numeroInteiro_(dados.fortitude, 0, 99, 0),
      atletismo: numeroInteiro_(dados.atletismo, 0, 99, 0),
      percepcao: numeroInteiro_(dados.percepcao, 0, 99, 0),
      historia: numeroInteiro_(dados.historia, 0, 99, 0),
      linguagem: numeroInteiro_(dados.linguagem, 0, 99, 0),
      pontaria: numeroInteiro_(dados.pontaria, 0, 99, 0),
      alquimia: numeroInteiro_(dados.alquimia, 0, 99, 0),
      vontade: numeroInteiro_(dados.vontade, 0, 99, 0),
      furtividade: numeroInteiro_(dados.furtividade, 0, 99, 0),
      instinto: numeroInteiro_(dados.instinto, 0, 99, 0),
      intimidacao: numeroInteiro_(dados.intimidacao, 0, 99, 0),
      enganacao: numeroInteiro_(dados.enganacao, 0, 99, 0),
      criadoEm: atual.criadoEm,
      atualizadoEm: agora_()
    };

    escreverLinha_(ABAS.PERSONAGENS, indice, salvo);
    return personagemSaida_(salvo);
  });
}

function excluirPersonagem_(corpo) {
  const usuario = exigirUsuario_(corpo);
  const id = texto_(corpo.id);
  if (!id) throw new ErroApi('Ficha sem identificador.');

  return comLock_(function () {
    const personagens = lerTabela_(ABAS.PERSONAGENS);
    const indice = personagens.findIndex(function (p) { return texto_(p.id) === id; });
    if (indice < 0) throw new ErroApi('Ficha não encontrada.');

    const dono = texto_(personagens[indice].usuarioId);
    if (texto_(usuario.papel) !== 'mestre' && dono !== texto_(usuario.id)) {
      throw new ErroApi('Você só pode excluir as próprias fichas.');
    }

    const itens = lerTabela_(ABAS.ITENS);
    for (let i = itens.length - 1; i >= 0; i--) {
      if (texto_(itens[i].personagemId) === id) {
        aba_(ABAS.ITENS).deleteRow(i + 2);
      }
    }

    aba_(ABAS.PERSONAGENS).deleteRow(indice + 2);
    return { ok: true, id: id };
  });
}

/* ------------------------------------------------------------------ */
/* Itens e habilidades                                                */
/* ------------------------------------------------------------------ */

function salvarItem_(corpo) {
  exigirMestre_(corpo);
  const dados = corpo.item;
  if (!dados || typeof dados !== 'object') throw new ErroApi('Item inválido.');

  const personagemId = texto_(dados.personagemId);
  if (!personagemId) throw new ErroApi('Escolha a ficha do item.');

  return comLock_(function () {
    const personagens = lerTabela_(ABAS.PERSONAGENS);
    if (!personagens.some(function (p) { return texto_(p.id) === personagemId; })) {
      throw new ErroApi('Ficha não encontrada.');
    }

    const itens = lerTabela_(ABAS.ITENS);
    const id = texto_(dados.id);
    const indice = id
      ? itens.findIndex(function (i) { return texto_(i.id) === id; })
      : -1;

    const atual = indice >= 0 ? itemSaida_(itens[indice]) : null;
    if (id && !atual) throw new ErroApi('Item não encontrado.');

    const nome = limitarTexto_(dados.nome, 80);
    if (!nome) throw new ErroApi('Dê um nome ao item.');

    const tipo = TIPOS_ITEM.indexOf(texto_(dados.tipo)) >= 0 ? texto_(dados.tipo) : 'Item';
    const agora = agora_();

    const salvo = {
      id: atual ? atual.id : Utilities.getUuid(),
      personagemId: personagemId,
      tipo: tipo,
      nome: nome,
      descricao: limitarTexto_(dados.descricao, 2000),
      valor: numeroDecimal_(dados.valor, -999999, 999999, 0),
      quantidade: numeroInteiro_(dados.quantidade, 1, 9999, 1),
      equipado: !!dados.equipado,
      criadoEm: atual ? atual.criadoEm : agora,
      atualizadoEm: agora
    };

    if (indice >= 0) {
      escreverLinha_(ABAS.ITENS, indice, salvo);
    } else {
      adicionarLinhas_(ABAS.ITENS, [salvo]);
    }

    return itemSaida_(salvo);
  });
}

function excluirItem_(corpo) {
  exigirMestre_(corpo);
  const id = texto_(corpo.id);
  if (!id) throw new ErroApi('Item sem identificador.');

  return comLock_(function () {
    const itens = lerTabela_(ABAS.ITENS);
    const indice = itens.findIndex(function (i) { return texto_(i.id) === id; });
    if (indice < 0) throw new ErroApi('Item não encontrado.');
    aba_(ABAS.ITENS).deleteRow(indice + 2);
    return { ok: true, id: id };
  });
}

/* ------------------------------------------------------------------ */
/* Bestiário                                                          */
/* ------------------------------------------------------------------ */

function salvarMonstro_(corpo) {
  exigirMestre_(corpo);
  const dados = corpo.monstro;
  if (!dados || typeof dados !== 'object') throw new ErroApi('Monstro inválido.');

  return comLock_(function () {
    const monstros = lerTabela_(ABAS.MONSTROS);
    const id = texto_(dados.id);
    const indice = id
      ? monstros.findIndex(function (m) { return texto_(m.id) === id; })
      : -1;

    const atual = indice >= 0 ? monstroSaida_(monstros[indice]) : null;
    if (id && !atual) throw new ErroApi('Monstro não encontrado.');

    const nome = limitarTexto_(dados.nome, 80);
    if (!nome) throw new ErroApi('Dê um nome ao monstro.');

    const agora = agora_();
    const salvo = {
      id: atual ? atual.id : Utilities.getUuid(),
      nome: nome,
      vida: numeroInteiro_(dados.vida, 0, 99999, 0),
      vidaMax: numeroInteiro_(dados.vidaMax, 0, 99999, 0),
      postura: numeroInteiro_(dados.postura, 0, 99999, 0),
      stamina: numeroInteiro_(dados.stamina, 0, 99999, 0),
      resistencia: numeroInteiro_(dados.resistencia, 0, 9999, 0),
      fisico: numeroInteiro_(dados.fisico, 0, 99, 0),
      mental: numeroInteiro_(dados.mental, 0, 99, 0),
      espiritual: numeroInteiro_(dados.espiritual, 0, 99, 0),
      notas: limitarTexto_(dados.notas, 3000),
      criadoEm: atual ? atual.criadoEm : agora,
      atualizadoEm: agora
    };

    if (indice >= 0) {
      escreverLinha_(ABAS.MONSTROS, indice, salvo);
    } else {
      adicionarLinhas_(ABAS.MONSTROS, [salvo]);
    }

    return monstroSaida_(salvo);
  });
}

function excluirMonstro_(corpo) {
  exigirMestre_(corpo);
  const id = texto_(corpo.id);
  if (!id) throw new ErroApi('Monstro sem identificador.');

  return comLock_(function () {
    const monstros = lerTabela_(ABAS.MONSTROS);
    const indice = monstros.findIndex(function (m) { return texto_(m.id) === id; });
    if (indice < 0) throw new ErroApi('Monstro não encontrado.');
    aba_(ABAS.MONSTROS).deleteRow(indice + 2);
    return { ok: true, id: id };
  });
}

/* ------------------------------------------------------------------ */
/* Rolagens                                                           */
/* ------------------------------------------------------------------ */

function salvarRolagem_(corpo) {
  const usuario = exigirUsuario_(corpo);
  const resultados = Array.isArray(corpo.resultados) ? corpo.resultados : [];
  if (!resultados.length) throw new ErroApi('Nenhum resultado para registrar.');
  if (resultados.length > MAX_RESULTADOS_ROLAGEM) {
    throw new ErroApi('Muitos resultados em uma única rolagem.');
  }

  const contexto = limitarTexto_(corpo.contexto, 140) || 'Mesa';
  const personagemId = texto_(corpo.personagemId);
  const mestre = texto_(usuario.papel) === 'mestre';

  if (personagemId) {
    const personagem = lerTabela_(ABAS.PERSONAGENS).filter(function (p) {
      return texto_(p.id) === personagemId;
    })[0];
    if (!personagem) throw new ErroApi('Ficha da rolagem não encontrada.');
    if (!mestre && texto_(personagem.usuarioId) !== texto_(usuario.id)) {
      throw new ErroApi('Você só pode rolar pelas próprias fichas.');
    }
  }

  const agora = agora_();
  const entradas = resultados.map(function (resultado) {
    const total = Number(resultado && resultado.total);
    return {
      id: Utilities.getUuid(),
      criadoEm: agora,
      usuarioId: texto_(usuario.id),
      autor: texto_(usuario.nome),
      contexto: contexto,
      personagemId: personagemId,
      expressao: limitarTexto_(resultado && resultado.expressao, 200),
      rotulo: limitarTexto_(resultado && resultado.rotulo, 120),
      resultado: isFinite(total) ? total : 0,
      detalhe: limitarTexto_(resultado && resultado.detalhe, 4000),
      sucesso: typeof (resultado && resultado.sucesso) === 'boolean' ? resultado.sucesso : ''
    };
  });

  comLock_(function () {
    adicionarLinhas_(ABAS.ROLAGENS, entradas);
  });

  return entradas.map(rolagemSaida_);
}

function excluirRolagem_(corpo) {
  exigirMestre_(corpo);
  const id = texto_(corpo.id);
  if (!id) throw new ErroApi('Rolagem sem identificador.');

  return comLock_(function () {
    const rolagens = lerTabela_(ABAS.ROLAGENS);
    const indice = rolagens.findIndex(function (r) { return texto_(r.id) === id; });
    if (indice < 0) throw new ErroApi('Rolagem não encontrada.');
    aba_(ABAS.ROLAGENS).deleteRow(indice + 2);
    return { ok: true, id: id };
  });
}

function limparRolagens_(corpo) {
  exigirMestre_(corpo);

  return comLock_(function () {
    const aba = aba_(ABAS.ROLAGENS);
    const ultima = aba.getLastRow();
    if (ultima > 1) aba.deleteRows(2, ultima - 1);
    return { ok: true };
  });
}

/* ------------------------------------------------------------------ */
/* Configuração                                                       */
/* ------------------------------------------------------------------ */

function salvarConfiguracao_(corpo) {
  exigirMestre_(corpo);
  const dados = corpo.config;
  if (!dados || typeof dados !== 'object') throw new ErroApi('Configuração inválida.');

  const nome = limitarTexto_(dados.nomeCampanha, 80) || 'Gates of Hell';

  comLock_(function () {
    const aba = aba_(ABAS.CONFIG);
    const linhas = aba.getLastRow() > 1
      ? aba.getRange(2, 1, aba.getLastRow() - 1, 2).getValues()
      : [];

    const valores = { nomeCampanha: nome };
    const encontrados = {};

    linhas.forEach(function (linha, indice) {
      const chave = texto_(linha[0]);
      if (Object.prototype.hasOwnProperty.call(valores, chave)) {
        aba.getRange(indice + 2, 2).setValue(valorCelula_(valores[chave]));
        encontrados[chave] = true;
      }
    });

    Object.keys(valores).forEach(function (chave) {
      if (!encontrados[chave]) aba.appendRow([chave, valorCelula_(valores[chave])]);
    });
  });

  return { nomeCampanha: nome };
}

function lerConfiguracao_() {
  const config = { nomeCampanha: 'Gates of Hell' };
  const aba = aba_(ABAS.CONFIG);
  if (aba.getLastRow() < 2) return config;

  const linhas = aba.getRange(2, 1, aba.getLastRow() - 1, 2).getValues();
  linhas.forEach(function (linha) {
    const chave = texto_(linha[0]);
    if (chave === 'nomeCampanha') {
      config.nomeCampanha = limitarTexto_(linha[1], 80) || 'Gates of Hell';
    }
  });

  return config;
}

/* ------------------------------------------------------------------ */
/* Planilha                                                           */
/* ------------------------------------------------------------------ */

function planilha_() {
  const id = PropertiesService.getScriptProperties().getProperty(PROP_PLANILHA);
  if (!id) {
    throw new ErroApi('Backend não configurado: execute setup() no editor do Apps Script.');
  }

  try {
    return SpreadsheetApp.openById(id);
  } catch (erro) {
    throw new ErroApi('A planilha do backend não está acessível. Confira se ela ainda existe.');
  }
}

function aba_(nome) {
  const aba = planilha_().getSheetByName(nome);
  if (!aba) {
    throw new ErroApi('A aba "' + nome + '" não existe. Execute setup() novamente.');
  }
  return aba;
}

function lerTabela_(nome) {
  const aba = aba_(nome);
  const ultima = aba.getLastRow();
  if (ultima < 2) return [];

  const headers = CABECALHOS[nome];
  const valores = aba.getRange(2, 1, ultima - 1, headers.length).getValues();
  const objetos = [];

  valores.forEach(function (linha) {
    if (!linha.some(function (v) { return v !== '' && v !== null; })) return;
    const objeto = {};
    headers.forEach(function (header, indice) {
      objeto[header] = linha[indice];
    });
    objetos.push(objeto);
  });

  return objetos;
}

function adicionarLinhas_(nome, objetos) {
  if (!objetos.length) return;
  const aba = aba_(nome);
  const headers = CABECALHOS[nome];
  const linhas = objetos.map(function (objeto) {
    return headers.map(function (header) {
      return valorCelula_(objeto[header]);
    });
  });

  const inicio = Math.max(aba.getLastRow() + 1, 2);
  aba.getRange(inicio, 1, linhas.length, headers.length).setValues(linhas);
}

function escreverLinha_(nome, indiceZero, objeto) {
  const aba = aba_(nome);
  const headers = CABECALHOS[nome];
  const valores = headers.map(function (header) {
    return valorCelula_(objeto[header]);
  });
  aba.getRange(indiceZero + 2, 1, 1, headers.length).setValues([valores]);
}

function excluirPorId_(nome, campo, valor) {
  const linhas = lerTabela_(nome);
  const indice = linhas.findIndex(function (linha) {
    return texto_(linha[campo]) === texto_(valor);
  });
  if (indice >= 0) aba_(nome).deleteRow(indice + 2);
}

function comLock_(funcao) {
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) {
    throw new ErroApi('A planilha está ocupada agora. Tente novamente em instantes.');
  }
  try {
    return funcao();
  } finally {
    lock.releaseLock();
  }
}

/* ------------------------------------------------------------------ */
/* Conversões                                                         */
/* ------------------------------------------------------------------ */

function personagemSaida_(o) {
  return {
    id: texto_(o.id),
    usuarioId: texto_(o.usuarioId),
    nome: texto_(o.nome),
    classe: texto_(o.classe),
    raca: texto_(o.raca),
    periciaClasse: texto_(o.periciaClasse),
    armadura: texto_(o.armadura),
    classeValor: numero_(o.classeValor, 0),
    vida: numero_(o.vida, 0),
    postura: numero_(o.postura, 0),
    stamina: numero_(o.stamina, 0),
    resistencia: numero_(o.resistencia, 0),
    fisico: numero_(o.fisico, 0),
    mental: numero_(o.mental, 0),
    espiritual: numero_(o.espiritual, 0),
    luta: numero_(o.luta, 0),
    iniciativa: numero_(o.iniciativa, 0),
    esquiva: numero_(o.esquiva, 0),
    fortitude: numero_(o.fortitude, 0),
    atletismo: numero_(o.atletismo, 0),
    percepcao: numero_(o.percepcao, 0),
    historia: numero_(o.historia, 0),
    linguagem: numero_(o.linguagem, 0),
    pontaria: numero_(o.pontaria, 0),
    alquimia: numero_(o.alquimia, 0),
    vontade: numero_(o.vontade, 0),
    furtividade: numero_(o.furtividade, 0),
    instinto: numero_(o.instinto, 0),
    intimidacao: numero_(o.intimidacao, 0),
    enganacao: numero_(o.enganacao, 0),
    criadoEm: texto_(o.criadoEm),
    atualizadoEm: texto_(o.atualizadoEm)
  };
}

function itemSaida_(o) {
  return {
    id: texto_(o.id),
    personagemId: texto_(o.personagemId),
    tipo: texto_(o.tipo) || 'Item',
    nome: texto_(o.nome),
    descricao: texto_(o.descricao),
    valor: numero_(o.valor, 0),
    quantidade: numero_(o.quantidade, 1),
    equipado: valorBooleano_(o.equipado),
    criadoEm: texto_(o.criadoEm),
    atualizadoEm: texto_(o.atualizadoEm)
  };
}

function monstroSaida_(o) {
  return {
    id: texto_(o.id),
    nome: texto_(o.nome),
    vida: numero_(o.vida, 0),
    vidaMax: numero_(o.vidaMax, 0),
    postura: numero_(o.postura, 0),
    stamina: numero_(o.stamina, 0),
    resistencia: numero_(o.resistencia, 0),
    fisico: numero_(o.fisico, 0),
    mental: numero_(o.mental, 0),
    espiritual: numero_(o.espiritual, 0),
    notas: texto_(o.notas),
    criadoEm: texto_(o.criadoEm),
    atualizadoEm: texto_(o.atualizadoEm)
  };
}

function rolagemSaida_(o) {
  return {
    id: texto_(o.id),
    criadoEm: texto_(o.criadoEm),
    usuarioId: texto_(o.usuarioId),
    autor: texto_(o.autor),
    contexto: texto_(o.contexto),
    personagemId: texto_(o.personagemId),
    expressao: texto_(o.expressao),
    rotulo: texto_(o.rotulo),
    resultado: numero_(o.resultado, 0),
    detalhe: texto_(o.detalhe),
    sucesso: sucessoSaida_(o.sucesso)
  };
}

function usuarioSaida_(o) {
  return {
    id: texto_(o.id),
    nome: texto_(o.nome),
    usuario: texto_(o.usuario),
    papel: texto_(o.papel) || 'jogador',
    criadoEm: texto_(o.criadoEm)
  };
}

function dadosPing_() {
  const configurado = !!PropertiesService.getScriptProperties().getProperty(PROP_PLANILHA);
  return {
    app: 'Gates of Hell',
    versao: '1.0',
    configurado: configurado,
    agora: agora_()
  };
}

function texto_(valor) {
  if (valor === null || valor === undefined) return '';
  return String(valor).trim();
}

function limitarTexto_(valor, limite) {
  const t = texto_(valor);
  return t.length > limite ? t.slice(0, limite) : t;
}

function numero_(valor, padrao) {
  if (valor === '' || valor === null || valor === undefined) return padrao;
  const n = Number(valor);
  return isFinite(n) ? n : padrao;
}

function numeroInteiro_(valor, minimo, maximo, padrao) {
  const n = Math.round(numero_(valor, padrao));
  return Math.min(maximo, Math.max(minimo, n));
}

function numeroDecimal_(valor, minimo, maximo, padrao) {
  const n = numero_(valor, padrao);
  return Math.min(maximo, Math.max(minimo, n));
}

function valorBooleano_(valor) {
  if (typeof valor === 'boolean') return valor;
  const t = texto_(valor).toLowerCase();
  return t === 'true' || t === 'sim' || t === '1';
}

function sucessoSaida_(valor) {
  if (typeof valor === 'boolean') return valor;
  const t = texto_(valor).toLowerCase();
  if (t === 'true') return true;
  if (t === 'false') return false;
  return null;
}

/**
 * Evita que textos digitados por usuários virem fórmulas ao abrir a planilha.
 * O apóstrofo inicial é o prefixo de texto do Sheets e não aparece na célula.
 */
function valorCelula_(valor) {
  if (typeof valor === 'string') {
    const t = valor.length > 45000 ? valor.slice(0, 45000) : valor;
    if (/^[=+\-@]/.test(t)) return "'" + t;
    return t;
  }
  return valor;
}

function agora_() {
  return new Date().toISOString();
}
