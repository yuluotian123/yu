using System;

namespace GameLogic
{
    public static class PersistentIdUtility
    {
        public static string GeneratePersistentId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static void EnsurePersistentId(IGameObject gameObject)
        {
            if (gameObject == null)
                return;

            if (!string.IsNullOrWhiteSpace(gameObject.PersistentId))
                return;

            gameObject.PersistentId = GeneratePersistentId();
        }
    }
}
