using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CodexQuotaPet;
class Check
{
 [STAThread] static void Main()
 {
  var app=new Application();
  string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexQuotaPet","position.json");
  string old=File.Exists(path)?File.ReadAllText(path):null;
  try
  {
   // Visible WPF windows may round dimensions to device pixels at the current DPI.
   var pet=new PetWindow();pet.SetPetScale(1);
   var root=(Grid)((Grid)((Viewbox)pet.Content).Child).Children[0];
   MenuItem Find(string text)=>root.ContextMenu.Items.OfType<MenuItem>().Single(x=>x.Header.ToString().StartsWith(text));
   Find("放大").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
   if(Math.Abs(pet.Width-156.2)>1)throw new Exception($"Menu enlarge failed: {pet.Width}");
   var loaded=new PetWindow();if(Math.Abs(loaded.Width-pet.Width)>1)throw new Exception("Persistence failed");
   Find("缩小").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
   if(Math.Abs(pet.Width-142)>1)throw new Exception("Menu shrink failed");
   pet.SetPetScale(100);if(Math.Abs(pet.Width-284)>1)throw new Exception("Upper bound failed");
   pet.SetPetScale(-100);if(Math.Abs(pet.Width-71)>1)throw new Exception("Lower bound failed");
   Find("恢复默认大小").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
   if(Math.Abs(pet.Width-142)>1||Math.Abs(pet.Height-214)>1)throw new Exception("Reset failed");
   pet.SetPetScale(1.5);pet.ResetPosition();
   if(Math.Abs(new PetWindow().Width-213)>1)throw new Exception("Position reset lost scale");
   Console.WriteLine("PASS real WPF menu enlarge/shrink/reset, range, persistence and position reset");
  }
  finally{if(old!=null)File.WriteAllText(path,old);else if(File.Exists(path))File.Delete(path);}
 }
}


