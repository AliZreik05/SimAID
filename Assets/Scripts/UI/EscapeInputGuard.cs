public static class EscapeInputGuard
{
    private static int handledFrame = -1;

    public static bool WasHandledThisFrame => handledFrame == UnityEngine.Time.frameCount;

    public static void MarkHandled()
    {
        handledFrame = UnityEngine.Time.frameCount;
    }
}
