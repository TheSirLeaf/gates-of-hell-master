using System.Text.Json.Serialization;

namespace GatesOfHell.Shared.Models;

public sealed class Usuario
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";

    [JsonPropertyName("usuario")]
    public string Login { get; set; } = "";

    public string Papel { get; set; } = "jogador";
    public string CriadoEm { get; set; } = "";

    [JsonIgnore]
    public bool EhMestre => string.Equals(Papel, "mestre", StringComparison.OrdinalIgnoreCase);
}

public sealed class Personagem
{
    public string Id { get; set; } = "";
    public string UsuarioId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Classe { get; set; } = "";
    public string Raca { get; set; } = "";
    public string PericiaClasse { get; set; } = "";
    public string Armadura { get; set; } = "";
    public double ClasseValor { get; set; }

    public int Vida { get; set; }
    public int Postura { get; set; }
    public int Stamina { get; set; }
    public int Resistencia { get; set; }

    public int Fisico { get; set; }
    public int Mental { get; set; }
    public int Espiritual { get; set; }

    public int Luta { get; set; }
    public int Iniciativa { get; set; }
    public int Esquiva { get; set; }
    public int Fortitude { get; set; }
    public int Atletismo { get; set; }

    public int Percepcao { get; set; }
    public int Historia { get; set; }
    public int Linguagem { get; set; }
    public int Pontaria { get; set; }
    public int Alquimia { get; set; }

    public int Vontade { get; set; }
    public int Furtividade { get; set; }
    public int Instinto { get; set; }
    public int Intimidacao { get; set; }
    public int Enganacao { get; set; }

    public string CriadoEm { get; set; } = "";
    public string AtualizadoEm { get; set; } = "";

    public int GetPericia(string chave) => chave switch
    {
        "luta" => Luta,
        "iniciativa" => Iniciativa,
        "esquiva" => Esquiva,
        "fortitude" => Fortitude,
        "atletismo" => Atletismo,
        "percepcao" => Percepcao,
        "historia" => Historia,
        "linguagem" => Linguagem,
        "pontaria" => Pontaria,
        "alquimia" => Alquimia,
        "vontade" => Vontade,
        "furtividade" => Furtividade,
        "instinto" => Instinto,
        "intimidacao" => Intimidacao,
        "enganacao" => Enganacao,
        _ => 0
    };

    public void SetPericia(string chave, int valor)
    {
        valor = Math.Clamp(valor, 0, 99);
        switch (chave)
        {
            case "luta": Luta = valor; break;
            case "iniciativa": Iniciativa = valor; break;
            case "esquiva": Esquiva = valor; break;
            case "fortitude": Fortitude = valor; break;
            case "atletismo": Atletismo = valor; break;
            case "percepcao": Percepcao = valor; break;
            case "historia": Historia = valor; break;
            case "linguagem": Linguagem = valor; break;
            case "pontaria": Pontaria = valor; break;
            case "alquimia": Alquimia = valor; break;
            case "vontade": Vontade = valor; break;
            case "furtividade": Furtividade = valor; break;
            case "instinto": Instinto = valor; break;
            case "intimidacao": Intimidacao = valor; break;
            case "enganacao": Enganacao = valor; break;
        }
    }

    [JsonIgnore]
    public IReadOnlyList<string> PericiasDaClasse =>
        PericiaClasse
            .Split(['|', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

    public int PericiasComPontos => Pericias.Todas.Count(p => GetPericia(p.Chave) > 0);

    public int LimitePericias => Math.Max(0, Mental) + PericiasDaClasse.Count;

    public Personagem Clone() => (Personagem)MemberwiseClone();
}

public sealed record PericiaDef(string Chave, string Nome, string Grupo);

public static class Pericias
{
    public const string GrupoFisicas = "Físicas";
    public const string GrupoMentais = "Mentais";
    public const string GrupoEspirituais = "Espirituais";

    public static readonly IReadOnlyList<PericiaDef> Todas =
    [
        new("luta", "Luta", GrupoFisicas),
        new("iniciativa", "Iniciativa", GrupoFisicas),
        new("esquiva", "Esquiva", GrupoFisicas),
        new("fortitude", "Fortitude", GrupoFisicas),
        new("atletismo", "Atletismo", GrupoFisicas),

        new("percepcao", "Percepção", GrupoMentais),
        new("historia", "História", GrupoMentais),
        new("linguagem", "Linguagem", GrupoMentais),
        new("pontaria", "Pontaria", GrupoMentais),
        new("alquimia", "Alquimia", GrupoMentais),

        new("vontade", "Vontade", GrupoEspirituais),
        new("furtividade", "Furtividade", GrupoEspirituais),
        new("instinto", "Instinto", GrupoEspirituais),
        new("intimidacao", "Intimidação", GrupoEspirituais),
        new("enganacao", "Enganação", GrupoEspirituais)
    ];

    public static readonly IReadOnlyList<string> Grupos =
        [GrupoFisicas, GrupoMentais, GrupoEspirituais];

    public static IEnumerable<PericiaDef> DoGrupo(string grupo) =>
        Todas.Where(p => p.Grupo == grupo);

    public static string NomeDaChave(string chave) =>
        Todas.FirstOrDefault(p => p.Chave == chave)?.Nome ?? chave;
}

public static class Testes
{
    public const int Lados = 20;
    public const int DadosMinimos = 1;

    public static int Dados(int atributo) => Math.Max(DadosMinimos, atributo);

    public static string Expressao(int atributo) => $"{Dados(atributo)}d{Lados}kh1";

    public static string AtributoDoGrupo(string grupo) => grupo switch
    {
        Pericias.GrupoFisicas => "Físico",
        Pericias.GrupoMentais => "Mental",
        Pericias.GrupoEspirituais => "Espiritual",
        _ => "Físico"
    };

    public static int ValorDoAtributo(Personagem personagem, string grupo) => grupo switch
    {
        Pericias.GrupoFisicas => personagem.Fisico,
        Pericias.GrupoMentais => personagem.Mental,
        Pericias.GrupoEspirituais => personagem.Espiritual,
        _ => personagem.Fisico
    };

    public static string ExpressaoDaPericia(Personagem personagem, PericiaDef pericia)
    {
        var expressao = Expressao(ValorDoAtributo(personagem, pericia.Grupo));
        var valor = personagem.GetPericia(pericia.Chave);
        return valor > 0 ? $"{expressao}+{valor}" : expressao;
    }
}

public static class ItemTipos
{
    public const string Item = "Item";
    public const string Equipamento = "Equipamento";
    public const string Habilidade = "Habilidade";

    public static readonly IReadOnlyList<string> Todos = [Equipamento, Item, Habilidade];
}

public sealed class ItemInventario
{
    public string Id { get; set; } = "";
    public string PersonagemId { get; set; } = "";
    public string Tipo { get; set; } = ItemTipos.Item;
    public string Nome { get; set; } = "";
    public string Descricao { get; set; } = "";
    public double Valor { get; set; }
    public int Quantidade { get; set; } = 1;
    public bool Equipado { get; set; }
    public string CriadoEm { get; set; } = "";
    public string AtualizadoEm { get; set; } = "";
}

public sealed class Classe
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Pericia1 { get; set; } = "";
    public string Pericia2 { get; set; } = "";
    public string Pericia3 { get; set; } = "";
    public string CriadoEm { get; set; } = "";
    public string AtualizadoEm { get; set; } = "";

    public IReadOnlyList<string> Pericias =>
        new[] { Pericia1, Pericia2, Pericia3 }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

    public Classe Clone() => (Classe)MemberwiseClone();
}

public sealed class Monstro
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public int Vida { get; set; }
    public int VidaMax { get; set; }
    public int Postura { get; set; }
    public int Stamina { get; set; }
    public int Resistencia { get; set; }
    public int Fisico { get; set; }
    public int Mental { get; set; }
    public int Espiritual { get; set; }
    public string Notas { get; set; } = "";
    public string CriadoEm { get; set; } = "";
    public string AtualizadoEm { get; set; } = "";

    public Monstro Clone() => (Monstro)MemberwiseClone();
}

public sealed class RollEntry
{
    public string Id { get; set; } = "";
    public string CriadoEm { get; set; } = "";
    public string UsuarioId { get; set; } = "";
    public string Autor { get; set; } = "";
    public string Contexto { get; set; } = "";
    public string PersonagemId { get; set; } = "";
    public string Expressao { get; set; } = "";
    public string Rotulo { get; set; } = "";
    public double Resultado { get; set; }
    public string Detalhe { get; set; } = "";
    public bool? Sucesso { get; set; }
}

public sealed class Config
{
    public string NomeCampanha { get; set; } = "Gates of Hell";

    public Config Clone() => (Config)MemberwiseClone();
}

public sealed class GameState
{
    public List<Personagem> Personagens { get; set; } = [];
    public List<ItemInventario> Itens { get; set; } = [];
    public List<Classe> Classes { get; set; } = [];
    public List<Monstro> Monstros { get; set; } = [];
    public List<RollEntry> Rolagens { get; set; } = [];
    public List<Usuario> Usuarios { get; set; } = [];
    public Config Config { get; set; } = new();
    public string ServerTime { get; set; } = "";
}

public sealed class AuthResult
{
    public string Token { get; set; } = "";
    public Usuario Usuario { get; set; } = new();
}

public sealed class ApiEnvelope<T>
{
    public bool Ok { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}

public sealed record SkillRollRequest(string Chave, string Nome, string Atributo, string Expressao);
