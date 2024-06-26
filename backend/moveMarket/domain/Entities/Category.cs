﻿using core.Entities.Base;

namespace domain.Entities;

public class Category : IEntity<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? NormalizedName { get; set; } = null!;

    public ICollection<Kit> Kits { get; set; } = null!;
}