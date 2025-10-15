using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class Categoria
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = null!;
    public virtual List<Producto> Productos { get; set; } = new();
}
