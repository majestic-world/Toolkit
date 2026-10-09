namespace L2Toolkit.Models;

/// <summary>
/// Item de ComboBox criado em código: a lógica lê <see cref="Id"/>; o controle exibe <see cref="Label"/>.
/// </summary>
public sealed record Option(string Id, string Label)
{
    public override string ToString() => Label;
}
