using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ChatOnLine
{
    public class MessageItemDataTemplateSelector: DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatContent)
            {
                if ((item as ChatContent).IsSelf)
                {
                    return App.Current.Resources["MyselfSendDataTemplate"] as DataTemplate;
                }
                else
                {
                    return App.Current.Resources["OtherSendDataTemplate"] as DataTemplate;
                }
            }

            return base.SelectTemplate(item, container);
        }
    }
}
