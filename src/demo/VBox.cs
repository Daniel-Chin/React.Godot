// META_PROGRAM: react.godot script. Do not remove this comment.  
using Godot;
using System;

public partial class VBox : VBoxContainer, IReactable
{
    [ReactProp]
    private int n_butt;

    // Don't edit! Generated by meta programming. 
    
    private ReactPolice police;

	public override void _Ready()
    {
        police = new ReactPolice(this);
    }

    public void React()
    {
        police.OnEnter();
        
        GD.Print("VBox react");

        foreach (var n in GetChildren())
		{
			n.QueueFree();
		}
		
        for (int i = 0; i < n_butt; i++)
        {
            Butt b = new();
            AddChild(b);
        }
        
        police.OnExit();
    }

    public void SetProps(
        // Don't edit! Generated by meta programming. 
        int n_butt_
    )
    {
        bool need_react = false;

        // Don't edit! Generated by meta programming. 
        if (n_butt != n_butt_) { n_butt = n_butt_; need_react = true; } 

        if (need_react)
        {
            React();
        }
    }
}
