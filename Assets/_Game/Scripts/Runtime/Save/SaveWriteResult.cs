namespace AlienDefense.Save
{
    /// <summary>Outcome of an atomic write attempt.</summary>
    public readonly struct SaveWriteResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }

        private SaveWriteResult(bool success, string errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }

        public static SaveWriteResult Ok()
        {
            return new SaveWriteResult(true, null);
        }

        public static SaveWriteResult Fail(string message)
        {
            return new SaveWriteResult(false, message);
        }
    }
}
