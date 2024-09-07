using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using H2MLauncher.UI.ViewModels;

using NHotkey.Wpf;

namespace H2MLauncher.UI.View
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static class InputBinding
    {
        private static readonly Dictionary<ShortcutViewModel, List<KeyBinding>> ShortcutKeyBindings = [];

        /// <summary>Returns the value of the attached property Gesture for the <paramref name="inputBinding"/>.</summary>
        /// <param name="inputBinding"><see cref="System.Windows.Input.InputBinding"/>  whose property value will be returned.</param>
        /// <returns>Property value <see cref="InputGesture"/>.</returns>
        public static InputGesture GetGesture(System.Windows.Input.InputBinding inputBinding)
        {
            return (InputGesture)inputBinding.GetValue(GestureProperty);
        }

        /// <summary>Sets the value of the Gesture attached property to <paramref name="inputBinding"/>.</summary>
        /// <param name="inputBinding"><see cref="System.Windows.Input.InputBinding"/> whose property is setting to a value..</param>
        /// <param name="value"><see cref="InputGesture"/> value for property.</param>
        public static void SetGesture(System.Windows.Input.InputBinding inputBinding, InputGesture value)
        {
            inputBinding.SetValue(GestureProperty, value);
        }

        /// <summary><see cref="DependencyProperty"/> for methods <see cref="GetGesture(System.Windows.Input.InputBinding)"/>
        /// and <see cref="SetGesture(System.Windows.Input.InputBinding, InputGesture)"/>.</summary>
        public static readonly DependencyProperty GestureProperty =
            DependencyProperty.RegisterAttached(
                nameof(GetGesture).Substring(3),
                typeof(InputGesture),
                typeof(InputBinding),
                new PropertyMetadata(null, OnGestureChanged));

        private static void OnGestureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not System.Windows.Input.InputBinding inputBinding)
                throw new NotImplementedException($"Implemented only for the \"{typeof(System.Windows.Input.InputBinding).FullName}\" class");

            if (e.NewValue is not InputGesture inputGesture)
            {
                return;
            }

            inputBinding.Gesture = inputGesture;
        }



        public static ShortcutViewModel GetShortcut(DependencyObject obj)
        {
            return (ShortcutViewModel)obj.GetValue(ShortcutProperty);
        }

        public static void SetShortcut(DependencyObject obj, ShortcutViewModel value)
        {
            obj.SetValue(ShortcutProperty, value);
        }

        // Using a DependencyProperty as the backing store for Shortcut.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShortcutProperty =
            DependencyProperty.RegisterAttached("Shortcut", typeof(ShortcutViewModel), typeof(InputBinding), new PropertyMetadata(null, OnShortcutChanged));


        private static void OnShortcutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not KeyBinding keyBinding)
                throw new NotImplementedException($"Implemented only for the \"{typeof(KeyBinding).FullName}\" class");

            if (e.OldValue is ShortcutViewModel oldViewModel)
            {
                if (ShortcutKeyBindings.TryGetValue(oldViewModel, out var oldKeyBindings))
                {
                    oldKeyBindings.Remove(keyBinding);
                }

                //BindingOperations.ClearBinding(d, GestureProperty);
                keyBinding.Gesture = null;
                BindingOperations.ClearBinding(d, HotkeyManager.RegisterGlobalHotkeyProperty);
            }

            if (e.NewValue is not ShortcutViewModel shortcutViewModel)
            {
                return;
            }

            if (ShortcutKeyBindings.TryGetValue(shortcutViewModel, out var keyBindings))
            {
                keyBindings.Add(keyBinding);
            }
            else
            {
                keyBindings = [keyBinding];
                ShortcutKeyBindings.Add(shortcutViewModel, keyBindings);
            }

            shortcutViewModel.PropertyChanged += ShortcutViewModel_PropertyChanged;
            keyBinding.Gesture = shortcutViewModel.KeyGesture;

            BindingOperations.SetBinding(keyBinding, HotkeyManager.RegisterGlobalHotkeyProperty, new Binding(nameof(ShortcutViewModel.IsHotkeyRegistered))
            {
                Source = shortcutViewModel
            });
        }

        private static void ShortcutViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ShortcutViewModel.KeyGesture) && e.PropertyName != nameof(ShortcutViewModel.IsHotkey))
            {
                return;
            }

            ShortcutViewModel viewModel = (ShortcutViewModel)sender!;
            if (viewModel.IsHotkey && ShortcutKeyBindings.TryGetValue(viewModel, out var keyBindings))
            {
                // register the hotkeys again when the binding is changed
                foreach (var keyBinding in keyBindings)
                {
                    BindingOperations.ClearBinding(keyBinding, HotkeyManager.RegisterGlobalHotkeyProperty);

                    keyBinding.Gesture = viewModel.KeyGesture;

                    BindingOperations.SetBinding(keyBinding, HotkeyManager.RegisterGlobalHotkeyProperty,
                        new Binding(nameof(ShortcutViewModel.IsHotkeyRegistered))
                        {
                            Source = viewModel
                        });
                }
            }
        }
    }
}
