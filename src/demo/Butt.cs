// META_PROGRAM: react.godot script. Do not remove this comment.  
using Godot;
using System;

public partial class Butt : Button, IReactable
{
    private State<bool> is_active;

    private ReactPolice police;

	public override void _Ready()
    {
        police = new ReactPolice(this);
        Reactor.Stain(police);
        is_active = new State<bool>(police, false);
    }

    public void React()
    {
        police.OnEnter();
        
        GD.Print("Butt react");

        SizeFlagsVertical = SizeFlags.ExpandFill;
        
        if (is_active.Get())
        {
            Text = "Click: On";
        }
        else
        {
            Text = "Click: Off";
        }

        void toggle()
        {
            is_active.Set(!is_active.Get());
        }
        police.UseEffect(() => {
            Pressed += toggle;
            return () => {
                Pressed -= toggle;
            };
        });
        
        police.OnExit();
    }

    public void SetProps(
        // Don't edit! Generated by meta programming. 
        
    )
    {
        bool need_react = false;

        // Don't edit! Generated by meta programming. 
        

        if (need_react)
        {
            React();
        }
    }
}