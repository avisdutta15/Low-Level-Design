// Component
public interface IPizza
{
    string GetDescription();
    double GetCost();
}

// Concrete Component
public class Margherita : IPizza
{
    public string GetDescription() => "Margherita";
    public double GetCost() => 5.00;
}

public class Farmhouse : IPizza
{
    public string GetDescription() => "Farmhouse";
    public double GetCost() => 6.00;
}

// Base Decorator
public abstract class PizzaDecorator : IPizza
{
    protected IPizza _pizza;
    protected PizzaDecorator(IPizza pizza) => _pizza = pizza;

    public virtual string GetDescription() => _pizza.GetDescription();
    public virtual double GetCost() => _pizza.GetCost();
}

// Concrete Decorators
public class CheeseDecorator : PizzaDecorator
{
    public CheeseDecorator(IPizza pizza) : base(pizza) { }
    public override string GetDescription() => _pizza.GetDescription() + ", Extra Cheese";
    public override double GetCost() => _pizza.GetCost() + 1.50;
}

public class PepperoniDecorator : PizzaDecorator
{
    public PepperoniDecorator(IPizza pizza) : base(pizza) { }
    public override string GetDescription() => _pizza.GetDescription() + ", Pepperoni";
    public override double GetCost() => _pizza.GetCost() + 2.00;
}

public class MushroomDecorator : PizzaDecorator
{
    public MushroomDecorator(IPizza pizza) : base(pizza) { }
    public override string GetDescription() => _pizza.GetDescription() + ", Mushrooms";
    public override double GetCost() => _pizza.GetCost() + 1.00;
}