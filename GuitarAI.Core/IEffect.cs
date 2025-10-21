namespace GuitarAI.Core
{
    /// <summary>
    /// Base interface for all audio effects
    /// </summary>
    public interface IEffect
    {
        /// <summary>
        /// Process audio samples in place
        /// </summary>
        /// <param name="buffer">Audio buffer (16-bit PCM)</param>
        /// <param name="offset">Start position in buffer</param>
        /// <param name="count">Number of bytes to process</param>
        void Process(byte[] buffer, int offset, int count);

        /// <summary>
        /// Process floating point samples
        /// </summary>
        /// <param name="samples">Array of samples (-1.0 to 1.0)</param>
        /// <param name="offset">Start position</param>
        /// <param name="count">Number of samples</param>
        void ProcessSamples(float[] samples, int offset, int count);

        /// <summary>
        /// Reset effect state
        /// </summary>
        void Reset();

        /// <summary>
        /// Effect name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether the effect is currently enabled
        /// </summary>
        bool Enabled { get; set; }
    }
}