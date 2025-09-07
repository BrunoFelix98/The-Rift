using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Galaxy : Entity
{
    public List<SystemEntity> Systems{ get; private set; } = new List<SystemEntity>();

    public Galaxy(string name) : base(name, EntityType.CELESTIAL) { }
}
[System.Serializable]
public class SystemEntity : Entity
{
    public List<Planet> Planets { get; private set; } = new List<Planet>();
    public List<Star> Stars { get; private set; } = new List<Star>();
    public List<Station> Stations { get; private set; } = new List<Station>();
    public List<Gate> Gates { get; private set; } = new List<Gate>();
    public List<AsteroidBelt> AsteroidBelts { get; private set; } = new List<AsteroidBelt>();

    public SystemEntity(string name) : base(name, EntityType.CELESTIAL) { }

    public List<ShipEntity> ShipsInSystem { get; private set; } = new List<ShipEntity>();
}

[System.Serializable]
public class Star : Entity
{
    public Star(string name) : base(name, EntityType.CELESTIAL) { }
}
[System.Serializable]
public class Planet : Entity
{
    public List<Moon> Moons { get; private set; } = new List<Moon>();
    public List<Resource> Resources { get; private set; } = new List<Resource>();
    public CelestialType type { get; private set; }

    public Planet(string name) : base(name, EntityType.CELESTIAL) { }
}
[System.Serializable]
public class Moon : Entity
{
    public List<Resource> Resources { get; private set; } = new List<Resource>();
    public List<Station> Stations { get; private set; } = new List<Station>();
    public CelestialType type { get; private set; }

    public Moon(string name) : base(name, EntityType.CELESTIAL) { }
}

[System.Serializable]
public class AsteroidBelt : Entity
{
    public List<Asteroid> Asteroids { get; private set; } = new List<Asteroid>();
    public CelestialType type { get; private set; }

    public AsteroidBelt(string name) : base(name, EntityType.CELESTIAL) { }
}

[System.Serializable]
public class Asteroid : Entity
{
    // Immutable or data model list of resources for this asteroid instance (quantities set externally)
    public List<Resource> Resources { get; private set; } = new List<Resource>();
    public CelestialType type { get; private set; }

    public Asteroid(string name) : base(name, EntityType.CELESTIAL) { }

    // You can add pure data/model methods here if needed, but no runtime state handling or Unity interaction
}

