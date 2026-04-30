namespace GameLogic
{
    /// <summary>
    /// Common input contract for character ability components.
    /// </summary>
    public interface ICharacterIntentAbility2D<TIntent> where TIntent : struct
    {
        /// <summary>Intent requested by a controller or AI this frame.</summary>
        TIntent RawIntent { get; }

        /// <summary>Intent approved for the ability to execute this frame.</summary>
        TIntent ApprovedIntent { get; }

        /// <summary>Writes the raw intent requested by a controller or AI.</summary>
        void SetIntent(TIntent intent);

        /// <summary>Writes the approved intent to execute.</summary>
        void ApproveIntent(TIntent intent);

        /// <summary>Clears both raw and approved frame intents.</summary>
        void ClearFrameIntents();
    }
}
