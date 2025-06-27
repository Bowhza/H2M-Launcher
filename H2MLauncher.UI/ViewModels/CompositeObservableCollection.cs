using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace H2MLauncher.UI.ViewModels
{
    public class CompositeReadOnlyObservableCollection<T> : INotifyCollectionChanged, IReadOnlyCollection<T>
    {
        private readonly List<IReadOnlyCollection<T>> _subCollections = [];

        public IReadOnlyList<IReadOnlyCollection<T>> SubCollections => _subCollections;

        public int Count => SubCollections.Sum(col => col.Count);


        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void AddSubCollection<TCollection>(TCollection subCollection)
            where TCollection : IReadOnlyCollection<T>, INotifyCollectionChanged
        {
            if (_subCollections.Contains(subCollection))
            {
                throw new InvalidOperationException("Sub collection already present");
            }

            subCollection.CollectionChanged += OnSubCollectionChanged;
            _subCollections.Add(subCollection);

            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        }

        public bool RemoveSubCollection(IReadOnlyCollection<T> subCollection)
        {
            int startIndex = FindStartIndexOfSource(subCollection);
            if (startIndex == -1)
            {
                return false;
            }

            if (_subCollections.Remove(subCollection))
            {
                ((INotifyCollectionChanged) subCollection).CollectionChanged -= OnSubCollectionChanged;                

                CollectionChanged?.Invoke(this, new(
                    NotifyCollectionChangedAction.Remove, 
                    subCollection.ToList(), 
                    startIndex));

                return true;
            }

            return false;
        }

        public void RemoveSubCollection(ObservableCollection<T> subCollection)
        {
            if (!_subCollections.Contains(subCollection))
            {
                throw new InvalidOperationException("Sub collection not found");
            }

            subCollection.CollectionChanged -= OnSubCollectionChanged;
            _subCollections.Remove(subCollection);
        }

        private int FindStartIndexOfSource(object? source)
        {
            if (source is not IReadOnlyCollection<T> subCollection)
            {
                return Count;
            }

            return SubCollections
                .TakeWhile(c => c != subCollection)
                .Sum(c => c.Count);
        }

        private void OnSubCollectionChanged(object? source, NotifyCollectionChangedEventArgs args)
        {
            int startIndex = FindStartIndexOfSource(source);

            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    CollectionChanged?.Invoke(this, new(args.Action, args.NewItems, startIndex + args.NewStartingIndex));
                    break;

                case NotifyCollectionChangedAction.Remove:
                    CollectionChanged?.Invoke(this, new(args.Action, args.OldItems, startIndex + args.OldStartingIndex));
                    break;

                case NotifyCollectionChangedAction.Reset:
                    CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
                    break;

                case NotifyCollectionChangedAction.Replace:
                    CollectionChanged?.Invoke(this, 
                        new(args.Action, args.OldItems!, args.NewItems!, startIndex + args.NewStartingIndex));
                    break;

                case NotifyCollectionChangedAction.Move:
                    CollectionChanged?.Invoke(this,
                        new(args.Action, args.NewItems!, startIndex + args.OldStartingIndex, startIndex + args.NewStartingIndex));
                    break;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _subCollections.SelectMany(col => col).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
