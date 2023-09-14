using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/*
A react node:  
- Implements IReactable.  
- Props are private and marked with [ReactProp].  
- States are private.  
- All other fields must be immutable after _Ready().  
- Has exactly two public methods: SetProps(...), and _Ready().  
- Its constructor takes no arguments.  
- In React(), access only props and self states, and nothing else.  
    - e.g. don't use system time.  

The python metaprogrammer makes sure 
- Populates SetProps(...) according to props.  
*/

[AttributeUsage(AttributeTargets.Field)]
public class ReactProp : Attribute { }

public interface IReactable
{
    public void React();
}

public class ReactPolice {
    public readonly IReactable reactable;
    public readonly Node node;
    public int Depth {get; private set;}
    public delegate void EffectCleaner();
    public delegate EffectCleaner Effect();
    private EffectCleaner effectCleaner;
    private Effect effect;

    public ReactPolice(IReactable reactNode)
    {
        reactable = reactNode;
        node = (Node) reactNode;
        CalcDepth();
    }

    private void CalcDepth()
    {
        Depth = node.GetPath().GetNameCount();
    }

    public void OnEnter()
    {
        CalcDepth();
        Reactor.Push(this);
        Reactor.Clean(this);
        if (effectCleaner is not null)
        {
            effectCleaner();
            effectCleaner = null;
        }
    }

    public void OnExit()
    {
        Reactor.Pop(this);
        if (effect is not null)
        {
            effectCleaner = effect();
            effect = null;
        }
    }

    public void UseEffect(Effect effect_)
    {
        effect = effect_;
    }
}

public static class Reactor
{
    public static bool DebugMode = false;
    public static SortedList<int, HashSet<ReactPolice>> Dirty;
    private static readonly Stack<ReactPolice> stack;
    private static bool init_ok = false;

    static Reactor()
    {
        Dirty = new SortedList<int, HashSet<ReactPolice>>();
        stack = new Stack<ReactPolice>();
    }

    public static void Init(IReactable root)
    {
        root.React();
        init_ok = true;
    }

    public static void Push(ReactPolice police)
    {
        if (DebugMode)
        {
            Assert(stack.Peek().Depth < police.Depth);
            stack.Push(police);
        }
    }

    public static void Pop(ReactPolice police)
    {
        if (DebugMode)
        {
            Assert(ReferenceEquals(stack.Pop(), police));
        }
    }

    public static void OnNewFrame()
    {
        Assert(init_ok);
        while (true)
        {
            ReactPolice dirtyPolice = null;
            lock (Dirty)
            {
                foreach (var (_, layer) in Dirty)
                {
                    if (layer.Count == 0)
                        continue;
                    dirtyPolice = layer.First();
                }
                if (dirtyPolice is null)
                    break;
                dirtyPolice.reactable.React();
            }
        }
    }

    public static void Clean(ReactPolice police)
    {
        HashSet<ReactPolice> layer;
        try
        {
            layer = Dirty[police.Depth];
        } 
        catch (KeyNotFoundException)
        {
            return;
        } 
        layer.Remove(police);
    }

    public static void Stain(ReactPolice police)
    {
        HashSet<ReactPolice> layer;
        try
        {
            layer = Dirty[police.Depth];
        } 
        catch (KeyNotFoundException)
        {
            layer = new HashSet<ReactPolice>();
            Dirty[police.Depth] = layer;
        }
        layer.Add(police);
    }

    public static void Assert(bool x)
    {
        if (!x)
            throw new AssertionFailed();
    }

    public class AssertionFailed : Exception { }

    public static void PrintDirty()
    {
        GD.Print("Dirty:");
        foreach (var (depth, layer) in Dirty)
        {
            GD.Print(" ", depth, ":");
            foreach (var police in layer)
            {
                GD.Print("   ", police);
            }
        }
    }
}

public class State<T>
{
    private T value;
    public readonly ReactPolice owner;
    public State(ReactPolice owner_, T default_value)
    {
        value = default_value;
        owner = owner_;
    }
    public T Get()
    {
        return value;
    }
    public void Set(T new_value)
    {
        lock (Reactor.Dirty)
        {
            value = new_value;
            Reactor.Stain(owner);
        }
    }
}
