namespace Framework.Pool
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}