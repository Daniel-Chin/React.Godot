// META_PROGRAM: react.godot script. Do not remove this comment.  
using Godot;
using System;

public partial class NBSlider : VSlider, IReactable
{
    [ReactProp]
    private int example_prop;

    private ReactPolice police;

	public override void _Ready()
    {
        police = new ReactPolice(this);
    }

    public void React()
    {
        police.OnEnter();
        
        // your code here
        
        police.OnExit();
    }

    public void SetProps(
        // Don't edit! Generated by meta programming. 
        int example_prop_
    )
    {
        bool need_react = false;

        // Don't edit! Generated by meta programming. 
        if (! example_prop.Equals(example_prop_)) { example_prop = example_prop_; need_react = true; } 

        if (need_react)
        {
            React();
        }
    }
}
