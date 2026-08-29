namespace CameraVision.Core;

/// <summary>
/// The 80 COCO class names the YOLO model can detect, with PT-BR display labels.
/// Duplicated from the pipeline's ClassLabels.cs (src/CameraVision/Annotation) — the
/// console app and this list must stay in sync until they share a project.
/// </summary>
public static class DetectableClasses
{
    public static string Translate(string className) =>
        _ptBr.TryGetValue(className, out var label) ? label : className;

    // Declared after _ptBr (see bottom of file): static initializers run in declaration order.
    public static IReadOnlyList<string> Names => _names;

    private static readonly Dictionary<string, string> _ptBr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["person"] = "pessoa",
        ["bicycle"] = "bicicleta",
        ["car"] = "carro",
        ["motorcycle"] = "moto",
        ["airplane"] = "avião",
        ["bus"] = "ônibus",
        ["train"] = "trem",
        ["truck"] = "caminhão",
        ["boat"] = "barco",
        ["traffic light"] = "semáforo",
        ["fire hydrant"] = "hidrante",
        ["stop sign"] = "placa de pare",
        ["parking meter"] = "parquímetro",
        ["bench"] = "banco",
        ["bird"] = "pássaro",
        ["cat"] = "gato",
        ["dog"] = "cachorro",
        ["horse"] = "cavalo",
        ["sheep"] = "ovelha",
        ["cow"] = "vaca",
        ["elephant"] = "elefante",
        ["bear"] = "urso",
        ["zebra"] = "zebra",
        ["giraffe"] = "girafa",
        ["backpack"] = "mochila",
        ["umbrella"] = "guarda-chuva",
        ["handbag"] = "bolsa",
        ["tie"] = "gravata",
        ["suitcase"] = "mala",
        ["frisbee"] = "frisbee",
        ["skis"] = "esquis",
        ["snowboard"] = "snowboard",
        ["sports ball"] = "bola",
        ["kite"] = "pipa",
        ["baseball bat"] = "taco de beisebol",
        ["baseball glove"] = "luva de beisebol",
        ["skateboard"] = "skate",
        ["surfboard"] = "prancha de surfe",
        ["tennis racket"] = "raquete de tênis",
        ["bottle"] = "garrafa",
        ["wine glass"] = "taça de vinho",
        ["cup"] = "copo",
        ["fork"] = "garfo",
        ["knife"] = "faca",
        ["spoon"] = "colher",
        ["bowl"] = "tigela",
        ["banana"] = "banana",
        ["apple"] = "maçã",
        ["sandwich"] = "sanduíche",
        ["orange"] = "laranja",
        ["broccoli"] = "brócolis",
        ["carrot"] = "cenoura",
        ["hot dog"] = "cachorro-quente",
        ["pizza"] = "pizza",
        ["donut"] = "rosquinha",
        ["cake"] = "bolo",
        ["chair"] = "cadeira",
        ["couch"] = "sofá",
        ["potted plant"] = "vaso de planta",
        ["bed"] = "cama",
        ["dining table"] = "mesa de jantar",
        ["toilet"] = "vaso sanitário",
        ["tv"] = "tv",
        ["laptop"] = "notebook",
        ["mouse"] = "mouse",
        ["remote"] = "controle remoto",
        ["keyboard"] = "teclado",
        ["cell phone"] = "celular",
        ["microwave"] = "micro-ondas",
        ["oven"] = "forno",
        ["toaster"] = "torradeira",
        ["sink"] = "pia",
        ["refrigerator"] = "geladeira",
        ["book"] = "livro",
        ["clock"] = "relógio",
        ["vase"] = "vaso",
        ["scissors"] = "tesoura",
        ["teddy bear"] = "urso de pelúcia",
        ["hair drier"] = "secador de cabelo",
        ["toothbrush"] = "escova de dentes",
    };

    private static readonly List<string> _names = [.. _ptBr.Keys];
}
