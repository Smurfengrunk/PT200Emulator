namespace InputHandler
{
    public enum UniKey
    {
        None,
        Enter,
        Backspace,
        Tab,
        Escape,
        Space,
        Break,
        Up,
        Down,
        Left,
        Right,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Å, Ä, Ö,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    }

    [Flags]
    public enum UniModifiers
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4
    }
}
