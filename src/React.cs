using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

[AttributeUsage(AttributeTargets.Field)]
public class ReactProp : Attribute { }
[AttributeUsage(AttributeTargets.Field)]
public class ReactState : Attribute { }

public interface IReactable
{
    public void React();
}

public class ReactPolice {
    public readonly IReactable Reactable;
    public readonly IHasDepth HasDepth;
    public int Depth {get; private set;}    // cache
    public delegate void EffectCleaner();
    public delegate EffectCleaner Effect();
    private readonly Queue<Effect> effects;
    private readonly Queue<EffectCleaner> effectCleaners;

    public ReactPolice(IReactable reactable_, IHasDepth hasDepth_)
    {
        Reactable = reactable_;
        HasDepth = hasDepth_;
        effects = new Queue<Effect>();
        effectCleaners = new Queue<EffectCleaner>();
        RecacheDepth();
        Reactor.Stain(this);
    }
    public ReactPolice(IReactable reactNode) : this(
        reactNode, new GodotNodeHasDepth((Node) reactNode)
    ) { }

    private void RecacheDepth()
    {
        Depth = HasDepth.DepthInTree();
    }

    public void OnEnter()
    {
        RecacheDepth();
        Reactor.Push(this);
        Reactor.Clean(this);
        foreach (var cleaner in effectCleaners)
        {
            cleaner();
        }
        effectCleaners.Clear();
    }

    public void OnExit()
    {
        Reactor.Pop(this);
        foreach (var effect in effects)
        {
            effectCleaners.Enqueue(effect());
        }
        effects.Clear();
    }

    public void UseEffect(Effect effect)
    {
        effects.Enqueue(effect);
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

    public static void Init()
    {
        init_ok = true;
    }

    public static void Push(ReactPolice police)
    {
        if (DebugMode)
        {
            Assert(
                stack.Count == 0 
            || 
                stack.Peek().Depth < police.Depth
            );
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
        lock (Dirty)
        {
            while (true)
            {
                ReactPolice dirtyPolice = null;
                foreach (var (_, layer) in Dirty)
                {
                    if (layer.Count == 0)
                        continue;
                    dirtyPolice = layer.First();
                    break;
                }
                if (dirtyPolice is null)
                    break;
                dirtyPolice.Reactable.React();
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

    public class AssertionFailed : Exception
    {
        public AssertionFailed() : base() { }
        public AssertionFailed(string message) : base(message) { }
    }

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
    private readonly List<ReactPolice> depender_subroots;
    public delegate void SetterType(T x);
    public State(T default_value)
    {
        value = default_value;
        depender_subroots = new List<ReactPolice>();
    }
    public State(ReactPolice owner, T default_value) : this(default_value)
    {
        depender_subroots.Add(owner);
    }
    public void UsedBy(ReactPolice depender_subroot)
    {   // react.js does this with useContext
        depender_subroots.Add(depender_subroot);
    }
    public T Get(ReactPolice depender_subroot)
    {
        if (
            Reactor.DebugMode
            && ! depender_subroots.Contains(depender_subroot)
        )
        {
            throw new Reactor.AssertionFailed();
        }
        return value;
    }
    public T GetFromNonUI()
    {
        return value;
    }
    public void Set(T new_value)
    {
        lock (Reactor.Dirty)
        {
            if (! EqualityComparer<T>.Default.Equals(value, new_value))
            {
                value = new_value;
                foreach (var depender_subroot in depender_subroots)
                {
                    Reactor.Stain(depender_subroot);
                }
            }
        }
    }
    public SetterType Setter()
    {
        return (T x) => {Set(x);};
    }
}

public interface IHasDepth
{
    public int DepthInTree();
}

public class GodotNodeHasDepth : IHasDepth
{   // for repersenting RenderingServer objects without scene tree
    private readonly Node node;
    public GodotNodeHasDepth(Node node_)
    {
        node = node_;
    }

    public int DepthInTree()
    {
        return node.GetPath().GetNameCount();
    }
}

public static class CallbackType
{
    public delegate void VoidVoid();
}
