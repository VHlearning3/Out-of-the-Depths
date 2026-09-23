// The seam between combat and hand art. SlashAttack calls PlaySlash(); the hand visuals decide how to show it.
public interface IHandAnimator
{
    void PlaySlash();
}

// Hand art that shows the two slashes differently (left-right sweep, up-down chop): SlashAttack calls this one instead
// when the hand animator implements it.
public interface IDirectionalHandAnimator : IHandAnimator
{
    void PlaySlash(SlashAttack.Direction direction);
}
