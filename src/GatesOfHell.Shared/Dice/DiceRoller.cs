using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GatesOfHell.Shared.Dice;

public sealed class DiceRollException : Exception
{
    public DiceRollException(string message) : base(message) { }
}

/// <summary>
/// Motor de dados compatível com a sintaxe V1 do Rollem (rollem.rocks/docs/syntax).
/// Suporta XdY, dF, explosões (! / !N), keep/drop (k, kh, kl, d, dl, dh), ns,
/// crítico (cN), repetição (N#), matemática (+ - * / e parênteses), modificador
/// por dado (++ / --), contagem (&lt;&lt; / &gt;&gt;), comparação (&lt; &lt;= &gt; &gt;= =)
/// e rolagens inline com rótulo ([d20+5 ataque]).
/// </summary>
public sealed class DiceRoller
{
    private const int MaxDadosPorPool = 100;
    private const int MaxLados = 1000;
    private const int MaxRepeticoes = 20;
    private const int MaxResultados = 50;

    public IReadOnlyList<RollResult> RollCommand(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            throw new DiceRollException("Digite uma rolagem, por exemplo: 2d6+3 dano.");

        var texto = entrada.Trim();
        if (texto.Length > 600)
            throw new DiceRollException("A rolagem é longa demais (máximo de 600 caracteres).");

        var resultados = new List<RollResult>();
        var segmentos = ExtrairSegmentos(texto);

        if (segmentos.Count > 0)
        {
            if (segmentos.Count > MaxResultados)
                throw new DiceRollException($"Muitas rolagens inline (máximo de {MaxResultados}).");

            foreach (var segmento in segmentos)
                resultados.AddRange(ParseComando(segmento));
        }
        else
        {
            resultados.AddRange(ParseComando(texto));
        }

        if (resultados.Count == 0)
            throw new DiceRollException("Nenhuma rolagem foi encontrada.");
        if (resultados.Count > MaxResultados)
            throw new DiceRollException($"Muitas rolagens de uma vez (máximo de {MaxResultados}).");

        return resultados;
    }

    private static List<string> ExtrairSegmentos(string texto)
    {
        var segmentos = new List<string>();
        var inicio = texto.IndexOf('[');
        if (inicio < 0)
        {
            if (texto.Contains(']'))
                throw new DiceRollException("Há um colchete de fechamento sem abertura.");
            return segmentos;
        }

        var pos = 0;
        while (pos < texto.Length)
        {
            var abre = texto.IndexOf('[', pos);
            if (abre < 0)
                break;

            var fecha = texto.IndexOf(']', abre + 1);
            if (fecha < 0)
                throw new DiceRollException("Colchete de rolagem inline não fechado.");

            var conteudo = texto.Substring(abre + 1, fecha - abre - 1).Trim();
            if (conteudo.Length == 0)
                throw new DiceRollException("Há um colchete de rolagem vazio.");

            segmentos.Add(conteudo);
            pos = fecha + 1;
        }

        return segmentos;
    }

    private static List<RollResult> ParseComando(string segmento)
    {
        var texto = segmento.Trim();
        if (texto.Length == 0)
            throw new DiceRollException("Rolagem vazia.");

        // Repetição no início: 6#4d6kh3
        var hash = -1;
        for (var i = 0; i < texto.Length; i++)
        {
            if (char.IsDigit(texto[i]))
                continue;
            if (texto[i] == '#')
                hash = i;
            break;
        }

        if (hash > 0 && texto[..hash].Trim().All(char.IsDigit))
        {
            if (!int.TryParse(texto[..hash].Trim(), out var vezes))
                throw new DiceRollException("Repetição inválida.");

            if (vezes < 1 || vezes > MaxRepeticoes)
                throw new DiceRollException($"Repita a rolagem entre 1 e {MaxRepeticoes} vezes.");

            var restante = texto[(hash + 1)..].Trim();
            if (restante.Length == 0)
                throw new DiceRollException("Faltou a rolagem depois do #.");

            var lista = new List<RollResult>();
            for (var i = 0; i < vezes; i++)
                lista.AddRange(ParseUnico(restante));

            return lista;
        }

        return ParseUnico(texto);
    }

    private static List<RollResult> ParseUnico(string texto)
    {
        var parser = new Parser(texto);
        var avaliacao = parser.ParseExpressao();

        var restante = texto[parser.Posicao..].Trim();
        if (restante.Length > 0 && "+-*/<>=#!".Contains(restante[0]))
        {
            throw new DiceRollException(
                $"Não entendi a parte \"{restante}\". Confira a expressão ou escreva um rótulo depois de um espaço.");
        }

        var rotulo = restante.Length > 100 ? restante[..100] : restante;
        return
        [
            new RollResult
            {
                Rotulo = rotulo,
                Expressao = avaliacao.Expressao,
                Total = Math.Round(avaliacao.Valor, 2),
                Detalhe = FormatarDados(avaliacao.Dados),
                Sucesso = avaliacao.Sucesso
            }
        ];
    }

    private static string FormatarDados(List<Dado> dados)
    {
        if (dados.Count == 0)
            return "";

        var partes = new List<string>(dados.Count);
        foreach (var dado in dados)
        {
            var valor = dado.Fate
                ? dado.Valor switch { > 0 => "+", < 0 => "-", _ => "0" }
                : FormatarNumero(dado.Valor);

            if (dado.Dropped)
                partes.Add($"~~{valor}~~");
            else if (dado.Critico)
                partes.Add($"**{valor}**");
            else
                partes.Add(valor);
        }

        return string.Join(", ", partes);
    }

    internal static string FormatarNumero(double valor)
    {
        if (Math.Abs(valor - Math.Round(valor)) < 0.000001)
            return ((long)Math.Round(valor)).ToString(CultureInfo.InvariantCulture);

        return valor.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private sealed class Parser
    {
        private readonly string _texto;
        private int _pos;

        public Parser(string texto) => _texto = texto;

        public int Posicao => _pos;

        public Avaliacao ParseExpressao() => ParseComparacao();

        private Avaliacao ParseComparacao()
        {
            var esquerda = ParseContagem();
            PularEspacos();

            string? operador = null;
            if (Casar("<=")) operador = "<=";
            else if (Casar(">=")) operador = ">=";
            else if (Casar("==")) operador = "=";
            else if (Casar("<")) operador = "<";
            else if (Casar(">")) operador = ">";
            else if (Casar("=")) operador = "=";

            if (operador is null)
                return esquerda;

            var direita = ParseContagem();
            var sucesso = operador switch
            {
                "<" => esquerda.Valor < direita.Valor,
                "<=" => esquerda.Valor <= direita.Valor,
                ">" => esquerda.Valor > direita.Valor,
                ">=" => esquerda.Valor >= direita.Valor,
                _ => Math.Abs(esquerda.Valor - direita.Valor) < 0.000001
            };

            var dados = new List<Dado>(esquerda.Dados);
            dados.AddRange(direita.Dados);

            return new Avaliacao
            {
                Valor = esquerda.Valor,
                Dados = dados,
                Expressao = $"{esquerda.Expressao} {operador} {direita.Expressao}",
                Sucesso = sucesso
            };
        }

        private Avaliacao ParseContagem()
        {
            var esquerda = ParseAditivo();
            PularEspacos();

            bool? maiorIgual = null;
            if (Casar(">>")) maiorIgual = true;
            else if (Casar("<<")) maiorIgual = false;

            if (maiorIgual is null)
                return esquerda;

            if (esquerda.Dados.Count == 0)
                throw new DiceRollException("A contagem (<< ou >>) precisa de uma rolagem de dados à esquerda.");

            var direita = ParseAditivo();
            var limite = direita.Valor;
            var quantidade = esquerda.Dados.Count(d =>
                !d.Dropped && (maiorIgual.Value ? d.Valor >= limite : d.Valor <= limite));

            return new Avaliacao
            {
                Valor = quantidade,
                Dados = esquerda.Dados,
                Expressao = $"{esquerda.Expressao} {(maiorIgual.Value ? ">>" : "<<")} {direita.Expressao}"
            };
        }

        private Avaliacao ParseAditivo()
        {
            var esquerda = ParseMultiplicativo();

            while (true)
            {
                PularEspacos();

                if (Casar("++"))
                {
                    var direita = ParseMultiplicativo();
                    esquerda = AplicarPorDado(esquerda, direita, 1);
                }
                else if (Casar("--"))
                {
                    var direita = ParseMultiplicativo();
                    esquerda = AplicarPorDado(esquerda, direita, -1);
                }
                else if (Casar("+"))
                {
                    var direita = ParseMultiplicativo();
                    esquerda = Combinar(esquerda, direita, '+');
                }
                else if (Casar("-"))
                {
                    var direita = ParseMultiplicativo();
                    esquerda = Combinar(esquerda, direita, '-');
                }
                else
                {
                    return esquerda;
                }
            }
        }

        private Avaliacao ParseMultiplicativo()
        {
            var esquerda = ParseUnario();

            while (true)
            {
                PularEspacos();

                if (Casar("*"))
                {
                    var direita = ParseUnario();
                    esquerda = Combinar(esquerda, direita, '*');
                }
                else if (Casar("/"))
                {
                    var direita = ParseUnario();
                    if (Math.Abs(direita.Valor) < 0.000001)
                        throw new DiceRollException("Divisão por zero.");
                    esquerda = Combinar(esquerda, direita, '/');
                }
                else
                {
                    return esquerda;
                }
            }
        }

        private Avaliacao ParseUnario()
        {
            PularEspacos();

            if (Casar("+"))
                return ParseUnario();

            if (Casar("-"))
            {
                var valor = ParseUnario();
                var dados = valor.Dados.Select(d => d.Copy()).ToList();
                foreach (var dado in dados)
                    dado.Valor = -dado.Valor;

                return new Avaliacao
                {
                    Valor = -valor.Valor,
                    Dados = dados,
                    Expressao = $"-{valor.Expressao}"
                };
            }

            return ParsePrimario();
        }

        private Avaliacao ParsePrimario()
        {
            PularEspacos();
            if (_pos >= _texto.Length)
                throw new DiceRollException("A expressão terminou antes da hora.");

            var atual = _texto[_pos];

            if (atual == '(')
            {
                _pos++;
                var interno = ParseExpressao();
                PularEspacos();
                if (!Casar(")"))
                    throw new DiceRollException("Faltou fechar um parêntese.");

                return new Avaliacao
                {
                    Valor = interno.Valor,
                    Dados = interno.Dados,
                    Expressao = $"({interno.Expressao})",
                    Sucesso = interno.Sucesso
                };
            }

            if (char.IsDigit(atual))
            {
                var inicio = _pos;
                while (_pos < _texto.Length && char.IsDigit(_texto[_pos]))
                    _pos++;

                var numeroTexto = _texto[inicio.._pos];
                if (!int.TryParse(numeroTexto, out var numero))
                    throw new DiceRollException("Número inválido.");

                // Só é dado se o d vem imediatamente depois (XdY). "4 d6" conta como número + rótulo.
                if (_pos < _texto.Length && (_texto[_pos] == 'd' || _texto[_pos] == 'D'))
                    return ParseDado(numero);

                return new Avaliacao
                {
                    Valor = numero,
                    Dados = [],
                    Expressao = numero.ToString(CultureInfo.InvariantCulture)
                };
            }

            if (atual == 'd' || atual == 'D')
                return ParseDado(1);

            throw new DiceRollException($"Não entendi o trecho \"{_texto[_pos..]}\".");
        }

        private Avaliacao ParseDado(int quantidade)
        {
            if (quantidade < 1)
                throw new DiceRollException("A quantidade de dados precisa ser pelo menos 1.");
            if (quantidade > MaxDadosPorPool)
                throw new DiceRollException($"No máximo {MaxDadosPorPool} dados por rolagem.");

            _pos++; // d

            var fate = false;
            var lados = 0;

            if (_pos < _texto.Length && (_texto[_pos] == 'f' || _texto[_pos] == 'F'))
            {
                fate = true;
                _pos++;
            }
            else
            {
                var inicio = _pos;
                while (_pos < _texto.Length && char.IsDigit(_texto[_pos]))
                    _pos++;

                if (inicio == _pos)
                    throw new DiceRollException("Faltou o tamanho do dado (por exemplo, d20).");

                if (!int.TryParse(_texto[inicio.._pos], out lados))
                    throw new DiceRollException("Tamanho de dado inválido.");

                if (lados < 2)
                    throw new DiceRollException("Dados de um lado só não existem.");
                if (lados > MaxLados)
                    throw new DiceRollException($"O dado pode ter no máximo {MaxLados} lados.");
            }

            var explode = false;
            var explodeEm = 0;
            var modo = ModoKeepDrop.Nenhum;
            var keepDropQuantidade = 0;
            var semOrdenar = false;
            int? criticoEm = null;

            // Sufixos são contíguos (sem espaço), como no Rollem: 10d6!kh5ns.
            if (!fate && !EspacoAtual() && Casar("!"))
            {
                explode = true;
                explodeEm = lados;

                if (_pos < _texto.Length && char.IsDigit(_texto[_pos]))
                {
                    var inicio = _pos;
                    while (_pos < _texto.Length && char.IsDigit(_texto[_pos]))
                        _pos++;

                    if (!int.TryParse(_texto[inicio.._pos], out explodeEm))
                        throw new DiceRollException("Limite de explosão inválido.");
                    if (explodeEm < 2 || explodeEm > lados)
                        throw new DiceRollException($"A explosão deve acontecer entre 2 e {lados}.");
                }
            }

            if (!fate)
            {
                if (!EspacoAtual() && TentarKeepDrop(out var modoLido, out var qtd))
                {
                    modo = modoLido;
                    keepDropQuantidade = Math.Clamp(qtd, 0, quantidade);
                }

                while (!EspacoAtual())
                {
                    if (Casar("ns"))
                    {
                        semOrdenar = true;
                        continue;
                    }

                    if (_pos + 1 < _texto.Length && (_texto[_pos] == 'c' || _texto[_pos] == 'C') &&
                        char.IsDigit(_texto[_pos + 1]))
                    {
                        _pos++;
                        var inicio = _pos;
                        while (_pos < _texto.Length && char.IsDigit(_texto[_pos]))
                            _pos++;
                        if (int.TryParse(_texto[inicio.._pos], out var valorCrit))
                            criticoEm = valorCrit;
                        continue;
                    }

                    break;
                }
            }

            var dados = new List<Dado>(quantidade);
            var fila = new Queue<Dado>();

            for (var i = 0; i < quantidade; i++)
                fila.Enqueue(RolarDado(fate, lados));

            while (fila.Count > 0 && dados.Count < MaxDadosPorPool)
            {
                var dado = fila.Dequeue();
                dados.Add(dado);

                if (explode && !fate && dado.Valor >= explodeEm && dados.Count < MaxDadosPorPool)
                    fila.Enqueue(RolarDado(false, lados));
            }

            AplicarKeepDrop(dados, modo, keepDropQuantidade);

            foreach (var dado in dados)
            {
                if (dado.Dropped)
                    continue;
                dado.Critico = criticoEm is not null
                    ? dado.Valor >= criticoEm.Value
                    : !fate && dado.Valor >= lados;
            }

            if (!semOrdenar)
            {
                dados = dados
                    .Select((dado, indice) => (dado, indice))
                    .OrderBy(par => par.dado.Valor)
                    .ThenBy(par => par.indice)
                    .Select(par => par.dado)
                    .ToList();
            }

            var valorTotal = dados.Where(d => !d.Dropped).Sum(d => d.Valor);
            var expressao = new StringBuilder();
            expressao.Append(quantidade.ToString(CultureInfo.InvariantCulture));
            expressao.Append(fate ? "dF" : $"d{lados}");

            if (explode)
            {
                expressao.Append('!');
                if (explodeEm != lados)
                    expressao.Append(explodeEm.ToString(CultureInfo.InvariantCulture));
            }

            expressao.Append(modo switch
            {
                ModoKeepDrop.KeepHighest => $"kh{keepDropQuantidade}",
                ModoKeepDrop.KeepLowest => $"kl{keepDropQuantidade}",
                ModoKeepDrop.DropHighest => $"dh{keepDropQuantidade}",
                ModoKeepDrop.DropLowest => $"dl{keepDropQuantidade}",
                _ => ""
            });

            if (semOrdenar)
                expressao.Append("ns");
            if (criticoEm is not null)
                expressao.Append('c').Append(criticoEm.Value.ToString(CultureInfo.InvariantCulture));

            return new Avaliacao
            {
                Valor = valorTotal,
                Dados = dados,
                Expressao = expressao.ToString()
            };
        }

        private bool TentarKeepDrop(out ModoKeepDrop modo, out int quantidade)
        {
            modo = ModoKeepDrop.Nenhum;
            quantidade = 0;

            if (_pos >= _texto.Length)
                return false;

            var atual = char.ToLowerInvariant(_texto[_pos]);

            if (atual == 'k')
            {
                var p = _pos + 1;
                var menor = false;

                if (p < _texto.Length && (char.ToLowerInvariant(_texto[p]) == 'h' || char.ToLowerInvariant(_texto[p]) == 'l'))
                {
                    menor = char.ToLowerInvariant(_texto[p]) == 'l';
                    p++;
                }

                if (p >= _texto.Length || !char.IsDigit(_texto[p]))
                    return false;

                var inicio = p;
                while (p < _texto.Length && char.IsDigit(_texto[p]))
                    p++;

                if (!int.TryParse(_texto[inicio..p], out quantidade))
                    return false;

                _pos = p;
                modo = menor ? ModoKeepDrop.KeepLowest : ModoKeepDrop.KeepHighest;
                return true;
            }

            if (atual == 'd')
            {
                var p = _pos + 1;
                var maior = false;

                if (p < _texto.Length && (char.ToLowerInvariant(_texto[p]) == 'h' || char.ToLowerInvariant(_texto[p]) == 'l'))
                {
                    maior = char.ToLowerInvariant(_texto[p]) == 'h';
                    p++;
                }

                if (p >= _texto.Length || !char.IsDigit(_texto[p]))
                    return false;

                var inicio = p;
                while (p < _texto.Length && char.IsDigit(_texto[p]))
                    p++;

                if (!int.TryParse(_texto[inicio..p], out quantidade))
                    return false;

                _pos = p;
                modo = maior ? ModoKeepDrop.DropHighest : ModoKeepDrop.DropLowest;
                return true;
            }

            return false;
        }

        private static void AplicarKeepDrop(List<Dado> dados, ModoKeepDrop modo, int quantidade)
        {
            if (modo == ModoKeepDrop.Nenhum || dados.Count == 0)
                return;

            var indices = Enumerable.Range(0, dados.Count).ToList();

            switch (modo)
            {
                case ModoKeepDrop.KeepHighest:
                {
                    var manter = indices.OrderByDescending(i => dados[i].Valor).ThenBy(i => i)
                        .Take(quantidade).ToHashSet();
                    for (var i = 0; i < dados.Count; i++)
                        dados[i].Dropped = !manter.Contains(i);
                    break;
                }
                case ModoKeepDrop.KeepLowest:
                {
                    var manter = indices.OrderBy(i => dados[i].Valor).ThenBy(i => i)
                        .Take(quantidade).ToHashSet();
                    for (var i = 0; i < dados.Count; i++)
                        dados[i].Dropped = !manter.Contains(i);
                    break;
                }
                case ModoKeepDrop.DropHighest:
                {
                    var descartar = indices.OrderByDescending(i => dados[i].Valor).ThenBy(i => i)
                        .Take(quantidade).ToHashSet();
                    for (var i = 0; i < dados.Count; i++)
                        dados[i].Dropped = descartar.Contains(i);
                    break;
                }
                case ModoKeepDrop.DropLowest:
                {
                    var descartar = indices.OrderBy(i => dados[i].Valor).ThenBy(i => i)
                        .Take(quantidade).ToHashSet();
                    for (var i = 0; i < dados.Count; i++)
                        dados[i].Dropped = descartar.Contains(i);
                    break;
                }
            }
        }

        private static Dado RolarDado(bool fate, int lados)
        {
            if (fate)
                return new Dado { Valor = RandomNumberGenerator.GetInt32(0, 3) - 1, Fate = true };

            return new Dado { Valor = RandomNumberGenerator.GetInt32(1, lados + 1) };
        }

        private static Avaliacao Combinar(Avaliacao esquerda, Avaliacao direita, char operador)
        {
            var dados = new List<Dado>(esquerda.Dados);
            dados.AddRange(direita.Dados);

            var valor = operador switch
            {
                '+' => esquerda.Valor + direita.Valor,
                '-' => esquerda.Valor - direita.Valor,
                '*' => esquerda.Valor * direita.Valor,
                '/' => esquerda.Valor / direita.Valor,
                _ => 0
            };

            return new Avaliacao
            {
                Valor = valor,
                Dados = dados,
                Expressao = $"{esquerda.Expressao} {operador} {direita.Expressao}"
            };
        }

        private static Avaliacao AplicarPorDado(Avaliacao esquerda, Avaliacao direita, int sinal)
        {
            if (direita.Dados.Count > 0)
                throw new DiceRollException("O modificador ++/-- precisa ser um número.");

            var delta = sinal * direita.Valor;
            var dados = esquerda.Dados.Select(d => d.Copy()).ToList();
            foreach (var dado in dados)
                dado.Valor += delta;

            var mantidos = dados.Count(d => !d.Dropped);
            var valor = esquerda.Valor + (mantidos > 0 ? delta * mantidos : delta);

            return new Avaliacao
            {
                Valor = valor,
                Dados = dados,
                Expressao = $"{esquerda.Expressao}{(sinal < 0 ? "--" : "++")}{FormatarNumero(direita.Valor)}"
            };
        }

        private void PularEspacos()
        {
            while (_pos < _texto.Length && char.IsWhiteSpace(_texto[_pos]))
                _pos++;
        }

        private bool Casar(string token)
        {
            if (_pos + token.Length > _texto.Length)
                return false;
            if (!_texto.AsSpan(_pos, token.Length).SequenceEqual(token.AsSpan()))
                return false;

            _pos += token.Length;
            return true;
        }

        private bool EspacoAtual() => _pos < _texto.Length && char.IsWhiteSpace(_texto[_pos]);

        private enum ModoKeepDrop
        {
            Nenhum,
            KeepHighest,
            KeepLowest,
            DropHighest,
            DropLowest
        }
    }

    private sealed class Avaliacao
    {
        public double Valor { get; set; }
        public List<Dado> Dados { get; set; } = [];
        public string Expressao { get; set; } = "";
        public bool? Sucesso { get; set; }
    }

    private sealed class Dado
    {
        public double Valor { get; set; }
        public bool Dropped { get; set; }
        public bool Critico { get; set; }
        public bool Fate { get; set; }

        public Dado Copy() => new()
        {
            Valor = Valor,
            Dropped = Dropped,
            Critico = Critico,
            Fate = Fate
        };
    }
}

public sealed class RollResult
{
    public string Rotulo { get; set; } = "";
    public string Expressao { get; set; } = "";
    public double Total { get; set; }
    public string Detalhe { get; set; } = "";
    public bool? Sucesso { get; set; }
}
