   M S B A M L   `   `   `  ���� E  AClearGlass, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null'clr-namespace:ClearGlass
ClearGlass      ClearGlass.MainWindow   W SPresentationCore, Version=6.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=6.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=6.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     *localclr-namespace:ClearGlass       � Title$  Clear Glass��$	��600��$	��500�� � WindowStartupLocation$ CenterScreen=�
������  � AllowsTransparency$
 True�� � WindowStyle$
 None=�$��1q�   GlassBorder#��[   � 	Resources    BooleanToVisibilityConverter,   &ClearGlass.StringToVisibilityConverter  StringToVisibilityConverter �� 
TargetType �� Property �� Value �� 
TargetType	 ��Name  GlassBackground
 �� CornerRadius J� Effect3  -System.Windows.Media.Effects.DropShadowEffect   ShadowDepth   
BlurRadius   Color  TextPrimary �� Triggers P� Property P� Value N� EnterActions �� 
TargetName �� TargetProperty	 �� To g� Duration	 t� To t� EasingFunction.  (System.Windows.Media.Animation.CubicEase7  1System.Windows.Media.Animation.EasingFunctionBase   
EasingMode N� ExitActions  ActionButton  PrimaryBlue  	LightBlue  DarkBlue 	 
TinyButton �� Padding � ColumnDefinitions ��Name �� Orientation �� ViewportHeight  �� ViewportSize! �� ScrollableHeight" � Maximum# �� VerticalOffset$ � Value*% �� #ComputedVerticalScrollBarVisibility& ]�Name' ]� Thumb( n�Name) �� Triggers 
 ShowAddonsOverlay* W� From	+ W� To, W� EasingFunction- t� From  HideAddonsOverlay  ShowOptimizationOverlay  HideOptimizationOverlay  ShowKeepAppsOverlay  HideKeepAppsOverlay  ShowTweaksOverlay  HideTweaksOverlay  ShowRemoveAppsOverlay  HideRemoveAppsOverlay��%�  &	       &	      'd� 
     7  7   7  '�� �    7  '� N    &	 w    7  7  7   7  7  &		 �    '�� �    &	
 	    &	 �	    &	 �
    &	 {    &	 Q    &	 !    &	 �    &	 �    &	 �    &	 O    ��   �� " d�.���  � ��$ 50����  � ��$ 26����  � �� �� " d�.R�� .+��� $	 BackgroundBorder��8��  $
 13��$��1q�8��    $ 0Q�$ 10Q�$ 	#40000000���� $	 Slider��$��20��$��20��$
 10��8�� $
��Left=�$��3,0,0,0q�   $ 0Q�$ 5Q�$ 	#40000000�� P�  �@d�	IsChecked$
 TrueY� �� .���� .���� $ BackgroundBorder��$1 +(Border.Background).(SolidColorBrush.Color)� #2196F3$ 0:0:0.2D�t� $ Slider��$ Margin� 27,0,0,0$ 0:0:0.2D�   $ EaseOut=� �� .���� .���� $ BackgroundBorder��$1 +(Border.Background).(SolidColorBrush.Color)� 	#20FFFFFF$ 0:0:0.2D�t� $ Slider��$ Margin� 3,0,0,0$ 0:0:0.2D�   $ EaseOut=��� " ��.���  � ��$ Center=���  � ��$ 14(���  � ��8   �� " �.���  � ��$ 0,0,0,20q��� " ��.���  � ��$ 40����  � ��8   ��  � ��8  ��  � ��$ 14(���  � ��$ SemiBold"���  � �� �� " ��.R��� #��z��$
 20��$��1q�8��    $ 0Q�$ 10Q�$ 	#40000000��._��� $��Center=�$��Center=�$
��15,5q� P�  �@J�IsMouseOver$
 True��. ��� 8   � ��P�  � ��$
 True��. ��� 8   � ���� " ��.���  � ��$ 28����  � ��
 ���� ��  � �� �	#AAFFFFFF��  � ��$ 11(���  � ��$ Normal"���  � �� �� " ��.R��� #��z��$
 14��$��1q����	#40FFFFFF$	 0,2q�._��� $��Center=�$��Center=�$	��8,2q� P�  �@J�IsMouseOver$
 True��. ���  � �� �	#20FFFFFF��  � �� �#FFFFFF��  � �� �	#60FFFFFFP�  � ��$
 True��. ���  � �� �	#30FFFFFF�� " ��.���  � �� �� " ��.R��  �� $��* ��� $
��Auto �.+��� $��0��#��z���� $ PART_VerticalScrollBar��$��1��$ Vertical=�#  z #" z! #$ z# #y�z% ���� " ��.���  � ��$ 8����  � ��
 ���� ��  � ��$ 0q���  � �� �� " ��.R�� .+�]� $& 
PART_Track��$
��True��' n� $( Thumb�����	#40FFFFFF$��0q����� " n�.���  � �� �� " n�.R��� #��z��$
 4��) P�  �@J�IsMouseOver$
 True��. ���  � �� �	#60FFFFFFP�  �@n�
IsDragging$
 True��. ���  � �� �	#80FFFFFF�� .��W� $ AddonsOverlay��$ Opacity�* 0+ 1$ 0:0:0.3D�,   $ EaseOut=�t� $ AddonsOverlay��$ Margin�- 0,600,0,-600 10,10,10,10$ 0:0:0.4D�   $ EaseOut=��� .��W� $ AddonsOverlay��$ Opacity�* 1+ 0$ 0:0:0.3D�,   $ EaseIn=�t� $ AddonsOverlay��$ Margin�- 10,10,10,10 0,600,0,-600$ 0:0:0.4D�   $ EaseIn=��� .��W� $ OptimizationOverlay��$ Opacity�* 0+ 1$ 0:0:0.3D�,   $ EaseOut=�t� $ OptimizationOverlay��$ Margin�- 0,600,0,-600 10,10,10,10$ 0:0:0.4D�   $ EaseOut=��� .��W� $ OptimizationOverlay��$ Opacity�* 1+ 0$ 0:0:0.3D�,   $ EaseIn=�t� $ OptimizationOverlay��$ Margin�- 10,10,10,10 0,600,0,-600$ 0:0:0.4D�   $ EaseIn=��� .��W� $ KeepAppsOverlay��$ Opacity�* 0+ 1$ 0:0:0.3D�,   $ EaseOut=�t� $ KeepAppsOverlay��$ Margin�- 0,600,0,-600 10,10,10,10$ 0:0:0.4D�   $ EaseOut=��� .��W� $ KeepAppsOverlay��$ Opacity�* 1+ 0$ 0:0:0.3D�,   $ EaseIn=�t� $ KeepAppsOverlay��$ Margin�- 10,10,10,10 0,600,0,-600$ 0:0:0.4D�   $ EaseIn=��� .��W� $ TweaksOverlay��$ Opacity�* 0+ 1$ 0:0:0.3D�,   $ EaseOut=�t� $ TweaksOverlay��$ Margin�- 0,600,0,-600 10,10,10,10$ 0:0:0.4D�   $ EaseOut=��� .��W� $ TweaksOverlay��$ Opacity�* 1+ 0$ 0:0:0.3D�,   $ EaseIn=�t� $ TweaksOverlay��$ Margin�- 10,10,10,10 0,600,0,-600$ 0:0:0.4D�   $ EaseIn=��� .��W� $ RemoveAppsOverlay��$ Opacity�* 0+ 1$ 0:0:0.3D�,   $ EaseOut=�t� $ RemoveAppsOverlay��$ Margin�- 0,600,0,-600 0$ 0:0:0.3D�   $ EaseOut=��� .��W� $ RemoveAppsOverlay��$ Opacity�* 1+ 0$ 0:0:0.3D�,   $ EaseOut=�t� $ RemoveAppsOverlay��$ Margin�- 0 0,600,0,-600$ 0:0:0.3D�   $ EaseOut=�.��� .+��� $
 10�����	#80000000$��1q�#��[  $��10q�   $ 15Q�$ 0Q�$ 	#40000000��._�� $��20,20,20,35q�. � RowDefinitions. �� $
��Auto ��� $
��Auto ��� $
��Auto ��� $
��Auto �.+�� $��0��$��30��$	��Top=�$��0,0,0,20q� �� $
��Auto ��� $��* ��� $
��Auto �.+��� $��Clear Glass��$��1��$��28(�$��Normal"�$:��4Gabriola, 'Segoe Script', 'Brush Script MT', Cursive)�#��[ $��Center=�$��Center=�   $ 0Q�$ 15Q�$ 	#40000000��/   Opacity$	/ 0.3Q��� -   0 ��Name$0 CloseButton��$��2��$	��✕��$��30��$��30��
������ $��0q�#��[ �� $��1��$��	40,0,40,0q�.+�� $��0,0,0,20q� �� $	��140 ��� $��40 ��� $	��120 ��� $
��Auto �.+��� $��Taskbar:��$��0��$��14(�$��SemiBold"�#��[    $ 0Q�$ 20Q�$ 	#40000000��$	/ 0.5Q��� $
��Left��$��1��$��Center=�$��0q�d� -   1 d�Name$1 TaskbarAlignmentToggle��$��2��$��Center=�$��0q��� $��Center��$��3��$��Center=�� $��0,0,0,20q� �� $	��140 ��� $��40 ��� $	��120 ��� $
��Auto �.+��� $��
Task View:��$��0��$��14(�$��SemiBold"�#��[    $ 0Q�$ 20Q�$ 	#40000000��$	/ 0.5Q��� $
��Hide��$��1��$��Center=�$��0q�d� -   $1 TaskViewToggle��$��2��$��Center=�$��0q��� $
��Show��$��3��$��Center=�� $��0,0,0,20q� �� $	��140 ��� $��40 ��� $	��120 ��� $	��140 �.+��� $��Search:��$��0��$��14(�$��SemiBold"�#��[    $ 0Q�$ 20Q�$ 	#40000000��$	/ 0.5Q��� $
��Hide��$��1��$��Center=�$��0q�d� -   $1 SearchToggle��$��2��$��Center=�$��0q��� $
��Icon��$��3��$��Center=�� $��0,0,0,20q� �� $	��140 ��� $��40 ��� $	��120 ��� $
��Auto �.+��� $��Desktop Icons:��$��0��$��14(�$��SemiBold"�#��[    $ 0Q�$ 20Q�$ 	#40000000��$	/ 0.5Q��� $
��Hide��$��1��$��Center=�$��0q�d� -   $1 DesktopIconsToggle��$��2��$��Center=�$��0q��� $
��Show��$��3��$��Center=�� $��0,0,0,20q� �� $	��140 ��� $��40 ��� $	��120 ��� $
��Auto �.+��� $��Theme:��$��0��$��14(�$��SemiBold"�#��[    $ 0Q�$ 20Q�$ 	#40000000��$	/ 0.5Q��� $��Light��$��1��$��Center=�$��0q�d� -   $1 ThemeToggle��$��2��$��Center=�$��0q��� $
��Dark��$��3��$��Center=�� $��2��$��	0,10,0,10q�. �� $
��Auto ��� $
��Auto ��� $
��Auto ��� $
��Auto � �� $��* ��� $��* �.+��� -   $0 RecommendedAddonsButton��$��0��$��2��$��Recommended Addons��#��[ $	��200��$��36��$��0,0,0,15q�$��Center=��� -   $0 ClearGlassThemeButton��$��Clear Glass Theme��$��1��$��0��#��[ $��	0,0,10,15q��� -	   $0 WindowsOptimizationButton��$��Windows Optimization��$��1��$��1��#��[ $��	10,0,0,15q��� -
   $0 ClearGlassButton��$��Run Clear Glass��$��2��$��2��#��[ $	��200��$��0,0,0,20q��� $��3��$��2��2 �� Orientation$2 
Horizontal=�$��Center=�$��Center=�$��0,5,0,10q�.+��� -   $0 AutoLoginButton��$��
Auto Login��#��[	 $��75��$��28��$��0,0,5,0q��� -   $0 KeepAppsButton��$��	Keep Apps��#��[	 $��75��$��28��$	��5,0q��� -   $0 TweaksButton��$��Tweaks��#��[	 $��75��$��28��$	��5,0q��� -   $0 RemoveAppsButton��$��Uninstaller��#��[	 $��75��$��28��$	��5,0q��� -   $0 SupportUsButton��$��
Support Us��#��[	 $��75��$��28��$��5,0,0,0q��� -   $	 AddonsOverlay��$
 10�����	#80000000$��1q�#��[  $��0,600,0,-600q�3 J� Opacity$3 0Q�4 K� ZIndex$
4 1000��   $ 15Q�$ 0Q�$ 	#40000000��._�� $��20q�. �� $
��Auto ��� $��* �.+�� $��30��$	��Top=�$��0,0,0,20q� �� $
��Auto ��� $��* ��� $
��Auto �.+��� $��Recommended Addons��$��1��$��28(�$��Normal"�$:��4Gabriola, 'Segoe Script', 'Brush Script MT', Cursive)�#��[ $��Center=�$��Center=�   $ 0Q�$ 15Q�$ 	#40000000��$	/ 0.3Q��� -   $0 CloseAddonsButton��$��2��$	��✕��$��30��$��30��
������ $��0q�#��[ � $��1��$��0,10,0,0q�. �� $��* ��� $
��Auto �.+��� $��0��$
��Auto=�$��Disabled=�$��0,0,5,0q�.���� .+��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��50 ��� $��* �.+��� $1��+pack://application:,,,/Images/librewolf.png��$��40��$��40��$	��Top=��� $��1��$��15,0,0,0q�.+��� $��	LibreWolf��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����LibreWolf is a privacy-focused web browser built from the Firefox source code. It aims to provide a more secure and private browsing experience by removing telemetry (data collection) and other non-essential features that could compromise user privacy.��$
��Wrap=�  TextSecondary#��[ 5 �� 
LineHeight$5 20���� -   $��Download��#��[	 $��80��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��50 ��� $��* �.+��� $-��'pack://application:,,,/Images/brave.png��$��40��$��40��$	��Top=��� $��1��$��15,0,0,0q�.+��� $��Brave Browser��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Brave is a privacy-focused web browser that blocks trackers and malicious ads by default. It features built-in privacy protection, a crypto wallet, and rewards system. The browser is designed to be fast, secure, and protect user privacy while maintaining compatibility with Chrome extensions.��$
��Wrap=�#��[ $5 20���� -   $��Download��#��[	 $��80��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��50 ��� $��* �.+��� $,��&pack://application:,,,/Images/revo.png��$��40��$��40��$	��Top=��� $��1��$��15,0,0,0q�.+��� $��Revo Uninstaller Pro��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Revo Uninstaller Pro is a powerful software tool designed to help users completely uninstall applications from Windows systems. Features advanced scanning modes, forced uninstall, real-time monitoring, and batch uninstallation. Ideal for: Users who frequently install and remove software, or who need to cleanly get rid of problematic programs.��$
��Wrap=�#��[ $5 20���� -   $��Download��#��[	 $��80��$��0,10,0,0q�$
��Left=��� -   $0 DownloadBundleButton��$��1��$��Download Bundle��#��[ $	��200��$��36��$��0,20,0,0q�$��Center=��� -   $	 OptimizationOverlay��$
 10�����	#80000000$��1q�#��[  $��0,600,0,-600q�$3 0Q�$
4 1000��   $ 15Q�$ 0Q�$ 	#40000000��._�� $��20q�. �� $
��Auto ��� $��* �.+�� $��30��$	��Top=�$��0,0,0,20q� �� $
��Auto ��� $��* ��� $
��Auto �.+��� $��Windows Optimization��$��1��$��28(�$��Normal"�$:��4Gabriola, 'Segoe Script', 'Brush Script MT', Cursive)�#��[ $��Center=�$��Center=�   $ 0Q�$ 15Q�$ 	#40000000��$	/ 0.3Q��� -   $0 CloseOptimizationButton��$��2��$	��✕��$��30��$��30��
������ $��0q�#��[ �� $��1��$
��Auto=�$��Disabled=�$��0,0,5,0q�.���� $	��Top=�$��Center=�$��0,0,0,10q�.+��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $#��Windows Settings Optimization��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����This will optimize various Windows settings including: disabling telemetry and data collection, optimizing visual effects for performance, adjusting power settings for better responsiveness, and configuring privacy settings. A system restore point will be created before making changes.��$
��Wrap=�#��[ $5 20���� -   $��Optimize Settings��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Remove Windows Bloatware��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����This will remove unnecessary Windows apps while keeping essential system components and useful applications. A system restore point will be created before making changes.��$
��Wrap=�#��[ $5 20���� -   $��Remove Bloatware��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Full System Optimization��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����This will perform a complete system optimization including Windows settings tweaks and bloatware removal. A system restore point will be created before making changes.��$
��Wrap=�#��[ $5 20���� -   $��Run Optimization��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� -   $	 KeepAppsOverlay��$
 10�����	#80000000$��1q�#��[  $��0,600,0,-600q�$3 0Q�$
4 1000��   $ 15Q�$ 0Q�$ 	#40000000��._�� $��20q�. �� $
��Auto ��� $
��Auto ��� $��* ��� $
��Auto �.+�� $��30��$	��Top=�$��0,0,0,20q� �� $
��Auto ��� $��* ��� $
��Auto �.+��� $!��Apps to Keep During Cleanup��$��1��$��28(�$��Normal"�$:��4Gabriola, 'Segoe Script', 'Brush Script MT', Cursive)�#��[ $��Center=�$��Center=�   $ 0Q�$ 15Q�$ 	#40000000��$	/ 0.3Q��� -   $0 CloseKeepAppsButton��$��2��$	��✕��$��30��$��30��
������ $��0q�#��[ �� $��1�����	#20FFFFFF$
 5��$��0,0,0,10q�$	��300��$��Center=�$��1q����	#40FFFFFF._��� -   6 ��Name$6 KeepAppsSearchBox��$
��10,5q�
������ $��0q�#��[ $��14(�7 �� 
CaretBrush
7 �����8 �� TextAlignment$
8 Left=�.������ " ��) P�  �@J�IsMouseOver$
 True��. ���  � �� �	#30FFFFFFP�  �@J�	IsFocused$
 True��. ���  � �� �	#30FFFFFF.������ " ��.R�� .+��� 9 ��Name$9 PART_ContentHost��$��0q�$��Center=��� $��Search applications...�����	#80FFFFFF$��14(�$
��Left=�$��Center=�$��10,0,0,0q�y��� *Text+: �� RelativeSource: �� *TemplatedParent+; �� 	Converter#; [ .���� $��2��$
��Auto=�$��Disabled=�$��0,0,5,0q�$��0,0,0,20q�.���� -   < ��Name$< AppsListView������ .R��� ���	#20FFFFFF$
 10��$��0,0,0,10q�$ 15q�._��  �� $
��Auto ��� $��* �.+��� $��Center=�$��0,0,15,0q�= d� 	IsChecked= �� *
IsSelected+�� $��1��.+��� $��16(�$��SemiBold"�#��[ $��0,0,0,5q����� *DisplayName+�� $
��Wrap=�#��[ $5 20������ *Id+�� -   $0 ApplyKeepAppsButton��$��3��$��Apply Changes��#��[ $	��200��$��36��$��Center=��� -    $	 TweaksOverlay��$
 10�����	#80000000$��1q�#��[  $��0,600,0,-600q�$3 0Q�$
4 1000��   $ 15Q�$ 0Q�$ 	#40000000��._�� $��20q�. �� $
��Auto ��� $��* �.+�� $��30��$	��Top=�$��0,0,0,20q� �� $
��Auto ��� $��* ��� $
��Auto �.+��� $��Windows Tweaks��$��1��$��28(�$��Normal"�$:��4Gabriola, 'Segoe Script', 'Brush Script MT', Cursive)�#��[ $��Center=�$��Center=�   $ 0Q�$ 15Q�$ 	#40000000��$	/ 0.3Q��� -!   $0 CloseTweaksButton��$��2��$	��✕��$��30��$��30��
������ $��0q�#��[ �� $��1��$
��Auto=�$��Disabled=�$��0,0,5,0q�.���� $	��Top=�$��Center=�$��0,0,0,10q�.+��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Remove OneDrive��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����This will completely remove Microsoft OneDrive from your system using multiple methods to ensure thorough removal. Before proceeding, make sure to stop OneDrive sync and back up any important files. This is a safe operation but requires proper preparation to avoid data loss.��$
��Wrap=�#��[ $5 20���� -"   $��Remove OneDrive��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $#��Sign Out of Microsoft Account��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Open the User Accounts section in Control Panel to manage your Microsoft account settings. To sign out, click 'Make changes to my account in PC settings' and then select 'Sign out'.��$
��Wrap=�#��[ $5 20���� -#   $��Open User Accounts��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Windows Settings��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $\��VOpen the modern Windows Settings app to configure your system preferences and options.��$
��Wrap=�#��[ $5 20���� -$   $��Open Settings��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Control Panel��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $W��QOpen the classic Windows Control Panel for advanced system configuration options.��$
��Wrap=�#��[ $5 20���� -%   $��Open Control Panel��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $ ��Disable Search Suggestions��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����This will disable web search suggestions and Bing integration in the Windows search box, improving privacy and reducing unnecessary network traffic.��$
��Wrap=�#��[ $5 20���� -&   $��Disable Suggestions��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Windows Image Backup��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Create a system image backup of your Windows installation. This creates a complete backup of your system drive that can be used to restore your PC.��$
��Wrap=�#��[ $5 20���� -'   $��Open Backup��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Registry Editor��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Open the Windows Registry Editor to view and modify system settings. Be cautious when making changes as they can affect system stability.��$
��Wrap=�#��[ $5 20���� -(   $��Open Registry Editor��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $!��Disable Privacy Permissions��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Disable personalized ads, local content based on language, app launch tracking, and suggested content in the settings app to enhance your privacy.��$
��Wrap=�#��[ $5 20���� -)   $��Disable Permissions��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $ ��Enable End Task in Taskbar��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Enable the ability to end tasks directly from the taskbar by right-clicking. This adds an 'End Task' option to the context menu when right-clicking taskbar items.��$
��Wrap=�#��[ $5 20���� -*   $��Enable End Task��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $��Set This PC as Default��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Set 'This PC' as the default view in File Explorer by removing Home and Gallery views. This provides a cleaner, more traditional file management experience.��$
��Wrap=�#��[ $5 20���� -+   $��Set This PC Default��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $��* �.+��� $��0��.+��� $"��Restore Classic Context Menu��$��16(�$��SemiBold"�#��[ $��0,0,0,5q��� $����Restore the classic (full) right-click context menu from Windows 10, removing the extra 'Show more options' step. This provides immediate access to all context menu items.��$
��Wrap=�#��[ $5 20���� -,   $��Restore Classic Menu��#��[	 $	��120��$��0,10,0,0q�$
��Left=��� --   $	 RemoveAppsOverlay��$
 10�����	#80000000$��1q�#��[  $��0,600,0,-600q�$3 0Q�$
4 1000��   $ 15Q�$ 0Q�$ 	#40000000��._�� $��20q�. �� $
��Auto ��� $
��Auto ��� $��* ��� $
��Auto �.+�� $��30��$	��Top=�$��0,0,0,20q� �� $
��Auto ��� $��* ��� $
��Auto �.+��� $��Uninstall Applications��$��1��$��28(�$��Normal"�$:��4Gabriola, 'Segoe Script', 'Brush Script MT', Cursive)�#��[ $��Center=�$��Center=�   $ 0Q�$ 15Q�$ 	#40000000��$	/ 0.3Q��� -.   $0 CloseRemoveAppsButton��$��2��$	��✕��$��30��$��30��
������ $��0q�#��[ �� $��1�����	#20FFFFFF$
 5��$��0,0,0,10q�$	��300��$��Center=�$��1q����	#40FFFFFF._��� -/   $6 AppSearchBox��$
��10,5q�
������ $��0q�#��[ $��14(�
7 �����$
8 Left=�.������ " ��) P�  �@J�IsMouseOver$
 True��. ���  � �� �	#30FFFFFFP�  �@J�	IsFocused$
 True��. ���  � �� �	#30FFFFFF.������ " ��.R�� .+��� $9 PART_ContentHost��$��0q�$��Center=��� $��Search applications...�����	#80FFFFFF$��14(�$
��Left=�$��Center=�$��10,0,0,0q�y��� *Text+: �� *TemplatedParent+#; [ .���� $��2��$
��Auto=�$��Disabled=�$��0,0,5,0q�$��0,0,0,20q�.���� -0   $< InstalledAppsList������ ���� .R��� ���	#20FFFFFF$
 10��$��0,0,0,15q�$ 15q�._��  �� $
��Auto ��� $��50 ��� $��* �.+��� $��0��$��Center=�$��0,0,15,0q�= �� *
IsSelected+�� $��1��$��40��$��40��$	��Top=����� *Icon+�� $��2��.+��� $��16(�$��SemiBold"�#��[ $��0,0,0,5q����� *Name+�� $
��Wrap=�#��[ $5 20������ *Id+�� $	3 0.7Q�$��12(�$��0,5,0,0q����� *	Version+�� -1   $0 UninstallAppsButton��$��3��$��Uninstall Selected��#��[ $	��200��$��36��$��Center=�