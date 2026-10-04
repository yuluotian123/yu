using System;
using System.Collections.Generic;

namespace Framework
{
    public static class ModuleSystem
    {
        internal const int DESIGN_MODULE_COUNT = 16;

        private static readonly Dictionary<Type, Module> _moduleMaps = new(DESIGN_MODULE_COUNT);
        private static readonly List<Module> _initializationOrder = new(DESIGN_MODULE_COUNT);
        private static readonly HashSet<Type> _initializingTypes = new();
        private static readonly List<IProcessModule> _processExecuteList = new(DESIGN_MODULE_COUNT);
        private static bool _isExecuteListDirty;
        private static bool _isShuttingDown;

        public static void Process(double elapseSeconds, double realElapseSeconds)
        {
            if (_isShuttingDown)
                return;
            if (_isExecuteListDirty)
            {
                _isExecuteListDirty = false;
                var modules = new List<Module>(_initializationOrder);
                // List.Sort is not stable, so retain initialization order for equal priorities.
                modules.Sort((left, right) =>
                {
                    int priority = right.Priority.CompareTo(left.Priority);
                    return priority != 0 ? priority :
                        _initializationOrder.IndexOf(left).CompareTo(_initializationOrder.IndexOf(right));
                });
                _processExecuteList.Clear();
                foreach (var module in modules)
                    if (module is IProcessModule process)
                        _processExecuteList.Add(process);
            }

            for (int i = 0; i < _processExecuteList.Count; i++)
                _processExecuteList[i].Process(elapseSeconds, realElapseSeconds);
        }

        public static void Shutdown()
        {
            if (_isShuttingDown)
                return;
            _isShuttingDown = true;
            try
            {
                RollbackFrom(0);
            }
            finally
            {
                _moduleMaps.Clear();
                _processExecuteList.Clear();
                _initializingTypes.Clear();
                _isExecuteListDirty = false;
                _isShuttingDown = false;
            }
        }

        public static T GetModule<T>() where T : class
        {
            Type contract = typeof(T);
            ValidateContract(contract);
            if (_moduleMaps.TryGetValue(contract, out var module))
                return (T)(object)module;
            if (_isShuttingDown)
                throw new InvalidOperationException($"Cannot create module '{contract}' during shutdown.");

            string name = contract.Namespace + "." + contract.Name.Substring(1);
            Type implementation = contract.Assembly.GetType(name);
            if (implementation == null || implementation.IsAbstract ||
                !typeof(Module).IsAssignableFrom(implementation) || !contract.IsAssignableFrom(implementation))
                throw new InvalidOperationException($"Cannot resolve module '{contract}' to '{name}'.");

            if (!_moduleMaps.TryGetValue(implementation, out module))
            {
                if (_initializingTypes.Contains(implementation))
                    throw new InvalidOperationException($"Circular module dependency: {implementation}.");
                module = (Module)Activator.CreateInstance(implementation);
                Initialize(module);
            }
            _moduleMaps.Add(contract, module);
            return (T)(object)module;
        }

        public static T RegisterModule<T>(Module module) where T : class
        {
            Type contract = typeof(T);
            ValidateContract(contract);
            ArgumentNullException.ThrowIfNull(module);
            if (!contract.IsInstanceOfType(module))
                throw new ArgumentException($"Module '{module.GetType()}' does not implement '{contract}'.", nameof(module));
            if (_isShuttingDown)
                throw new InvalidOperationException("Cannot register a module during shutdown.");

            if (_moduleMaps.TryGetValue(contract, out var registered))
            {
                if (!ReferenceEquals(registered, module))
                    throw new InvalidOperationException($"Module '{contract}' is already registered.");
                return (T)(object)module;
            }
            if (_moduleMaps.TryGetValue(module.GetType(), out registered))
            {
                if (!ReferenceEquals(registered, module))
                    throw new InvalidOperationException($"Module '{module.GetType()}' is already registered.");
            }
            else
                Initialize(module);

            _moduleMaps.Add(contract, module);
            return (T)(object)module;
        }

        private static void Initialize(Module module)
        {
            Type type = module.GetType();
            if (!_initializingTypes.Add(type))
                throw new InvalidOperationException($"Circular module dependency: {type}.");
            int checkpoint = _initializationOrder.Count;
            try
            {
                module.OnInit();
                _moduleMaps.Add(type, module);
                _initializationOrder.Add(module);
                _isExecuteListDirty = true;
            }
            catch
            {
                bool wasShuttingDown = _isShuttingDown;
                _isShuttingDown = true;
                try
                {
                    TryShutdown(module);
                    RollbackFrom(checkpoint);
                }
                finally { _isShuttingDown = wasShuttingDown; }
                throw;
            }
            finally { _initializingTypes.Remove(type); }
        }

        private static void RollbackFrom(int checkpoint)
        {
            for (int i = _initializationOrder.Count - 1; i >= checkpoint; i--)
            {
                Module module = _initializationOrder[i];
                TryShutdown(module);
                var keys = new List<Type>();
                foreach (var entry in _moduleMaps)
                    if (ReferenceEquals(entry.Value, module))
                        keys.Add(entry.Key);
                foreach (var key in keys)
                    _moduleMaps.Remove(key);
                _initializationOrder.RemoveAt(i);
            }
            _isExecuteListDirty = true;
        }

        private static void TryShutdown(Module module)
        {
            try { module.Shutdown(); }
            catch (Exception exception)
            {
                Debugger.Warn($"[ModuleSystem] Shutdown failed for '{module.GetType()}': {exception.Message}");
            }
        }

        private static void ValidateContract(Type contract)
        {
            if (!contract.IsInterface)
                throw new ArgumentException($"Modules must be requested by interface: {contract}.");
        }
    }
}
