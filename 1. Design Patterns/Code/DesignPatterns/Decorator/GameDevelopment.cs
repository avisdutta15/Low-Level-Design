// Component
public interface ICharacter
{
    string GetDescription();
    double GetAttack();
    double GetDefense();
    double GetSpeed();
}

// Concrete Component
public class BasicCharacter : ICharacter
{
    public string GetDescription() => "Basic Character";
    public double GetAttack() => 10.0;
    public double GetDefense() => 5.0;
    public double GetSpeed() => 8.0;
}

// Base Decorator
public abstract class CharacterDecorator : ICharacter
{
    protected ICharacter _character;
    protected CharacterDecorator(ICharacter character) => _character = character;

    public virtual string GetDescription() => _character.GetDescription();
    public virtual double GetAttack() => _character.GetAttack();
    public virtual double GetDefense() => _character.GetDefense();
    public virtual double GetSpeed() => _character.GetSpeed();
}

// Concrete Decorators - Abilities
public class FireAbilityDecorator : CharacterDecorator
{
    public FireAbilityDecorator(ICharacter character) : base(character) { }

    public override string GetDescription() => _character.GetDescription() + " with Fire Ability";
    public override double GetAttack() => _character.GetAttack() + 15.0;
}

public class IceAbilityDecorator : CharacterDecorator
{
    public IceAbilityDecorator(ICharacter character) : base(character) { }

    public override string GetDescription() => _character.GetDescription() + " with Ice Ability";
    public override double GetDefense() => _character.GetDefense() + 10.0;
}

// Concrete Decorators - Equipment
public class SwordDecorator : CharacterDecorator
{
    public SwordDecorator(ICharacter character) : base(character) { }

    public override string GetDescription() => _character.GetDescription() + " with Sword";
    public override double GetAttack() => _character.GetAttack() + 8.0;
    public override double GetSpeed() => _character.GetSpeed() - 1.0;
}

public class ShieldDecorator : CharacterDecorator
{
    public ShieldDecorator(ICharacter character) : base(character) { }

    public override string GetDescription() => _character.GetDescription() + " with Shield";
    public override double GetDefense() => _character.GetDefense() + 12.0;
    public override double GetSpeed() => _character.GetSpeed() - 2.0;
}

public class BootsDecorator : CharacterDecorator
{
    public BootsDecorator(ICharacter character) : base(character) { }

    public override string GetDescription() => _character.GetDescription() + " with Speed Boots";
    public override double GetSpeed() => _character.GetSpeed() + 5.0;
}