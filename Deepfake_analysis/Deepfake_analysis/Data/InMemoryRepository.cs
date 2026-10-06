using Deepfake_analysis.Models;

namespace Deepfake_analysis.Data
{
    public class InMemoryRepository<T> where T : class, IEntity
    {
        private readonly List<T> _items = [];
        private readonly Lock _lock = new();
        private int _nextId = 1;

        /// <summary>Возвращает снимок коллекции, по которому безопасно выполнять LINQ-запросы.</summary>
        public List<T> GetAll()
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }

        public T? Find(int id)
        {
            lock (_lock)
            {
                return _items.FirstOrDefault(x => x.Id == id);
            }
        }

        public T Add(T item)
        {
            lock (_lock)
            {
                item.Id = _nextId++;
                _items.Add(item);
                return item;
            }
        }

        public int RemoveAll(Predicate<T> match)
        {
            lock (_lock)
            {
                return _items.RemoveAll(match);
            }
        }
    }
}
