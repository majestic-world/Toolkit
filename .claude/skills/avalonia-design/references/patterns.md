# Page Layout Patterns — AXAML

---

## App Shell (Sidebar + TopBar + Content)

The standard enterprise shell: fixed sidebar on the left, a topbar, and a scrollable content area.

```xml
<!-- MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="YourApp.MainWindow"
        Width="1280" Height="800"
        MinWidth="900" MinHeight="600"
        Background="{DynamicResource BrushSurfaceBackground}"
        ExtendClientAreaToDecorationsHint="True">

  <Grid ColumnDefinitions="240,*">

    <!-- Sidebar -->
    <Border Grid.Column="0"
            Background="{DynamicResource BrushSurfaceCard}"
            BorderBrush="{DynamicResource BrushSurfaceBorder}"
            BorderThickness="0,0,1,0">
      <DockPanel>

        <!-- Logo area -->
        <Border DockPanel.Dock="Top" Padding="20,24,20,16">
          <StackPanel Orientation="Horizontal" Spacing="10">
            <!-- Replace with your app icon -->
            <Border Width="28" Height="28" CornerRadius="6"
                    Background="{DynamicResource BrushAccentPrimary}"/>
            <TextBlock Text="AppName"
                       FontSize="{DynamicResource FontSizeSubtitle}"
                       FontWeight="SemiBold"
                       Foreground="{DynamicResource BrushContentPrimary}"
                       VerticalAlignment="Center"/>
          </StackPanel>
        </Border>

        <!-- Nav section label -->
        <TextBlock DockPanel.Dock="Top"
                   Text="MAIN"
                   FontSize="{DynamicResource FontSizeSmall}"
                   FontWeight="Medium"
                   Foreground="{DynamicResource BrushContentTertiary}"
                   Margin="20,16,20,4"/>

        <!-- Nav items -->
        <StackPanel DockPanel.Dock="Top" Spacing="2" Margin="12,0">
          <Button Theme="{StaticResource NavItem}" Classes="NavItemActive">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <PathIcon Width="16" Height="16" Data="M3 12L12 3L21 12V21H15V15H9V21H3V12Z"/>
              <TextBlock Text="Dashboard"/>
            </StackPanel>
          </Button>
          <Button Theme="{StaticResource NavItem}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <PathIcon Width="16" Height="16" Data="M9 5H7A2 2 0 005 7V19A2 2 0 007 21H17A2 2 0 0019 19V7A2 2 0 0017 5H15M9 5A2 2 0 0011 3H13A2 2 0 0115 5M9 5A2 2 0 009 7H15A2 2 0 0015 5"/>
              <TextBlock Text="Projects"/>
            </StackPanel>
          </Button>
          <Button Theme="{StaticResource NavItem}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <PathIcon Width="16" Height="16" Data="M12 4.354A4 4 0 1110.161 6.5M15 21H3V20A6 6 0 0115 14M17 21V16M17 16V11M17 16H12M17 16H22"/>
              <TextBlock Text="Team"/>
            </StackPanel>
          </Button>
        </StackPanel>

        <!-- Bottom user area -->
        <Border DockPanel.Dock="Bottom"
                BorderBrush="{DynamicResource BrushSurfaceBorder}"
                BorderThickness="0,1,0,0"
                Padding="16,12">
          <Grid ColumnDefinitions="36,*,Auto">
            <Ellipse Grid.Column="0" Width="32" Height="32"
                     Fill="{DynamicResource BrushAccentSubtle}"/>
            <StackPanel Grid.Column="1" Margin="10,0,0,0" VerticalAlignment="Center" Spacing="1">
              <TextBlock Text="John Doe"
                         FontSize="{DynamicResource FontSizeBody}"
                         FontWeight="Medium"
                         Foreground="{DynamicResource BrushContentPrimary}"/>
              <TextBlock Text="Administrator"
                         FontSize="{DynamicResource FontSizeSmall}"
                         Foreground="{DynamicResource BrushContentSecondary}"/>
            </StackPanel>
          </Grid>
        </Border>

      </DockPanel>
    </Border>

    <!-- Main area -->
    <DockPanel Grid.Column="1">

      <!-- TopBar -->
      <Border DockPanel.Dock="Top"
              Background="{DynamicResource BrushSurfaceCard}"
              BorderBrush="{DynamicResource BrushSurfaceBorder}"
              BorderThickness="0,0,0,1"
              Height="56"
              Padding="24,0">
        <Grid ColumnDefinitions="*,Auto">
          <TextBlock Text="Dashboard"
                     FontSize="{DynamicResource FontSizeSubtitle}"
                     FontWeight="SemiBold"
                     Foreground="{DynamicResource BrushContentPrimary}"
                     VerticalAlignment="Center"/>
          <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
            <!-- Search -->
            <Border Width="240" Height="32"
                    Background="{DynamicResource BrushSurfaceBackground}"
                    BorderBrush="{DynamicResource BrushSurfaceBorder}"
                    BorderThickness="1"
                    CornerRadius="{DynamicResource RadiusS}"
                    Padding="10,0">
              <TextBox BorderThickness="0" Background="Transparent"
                       Watermark="Search..." FontSize="13"
                       VerticalContentAlignment="Center"/>
            </Border>
            <Button Theme="{StaticResource GhostButton}" Padding="8">
              <PathIcon Width="18" Height="18"
                        Data="M15 17H20L18.595 15.595C18.884 14.749 19 13.853 19 13C19 9.134 15.866 6 12 6C8.134 6 5 9.134 5 13C5 16.866 8.134 20 12 20C13.742 20 15.33 19.349 16.542 18.27L15 17ZM13 13V9H11V14H16V13H13Z"/>
            </Button>
          </StackPanel>
        </Grid>
      </Border>

      <!-- Content -->
      <ScrollViewer VerticalScrollBarVisibility="Auto">
        <ContentControl Content="{Binding CurrentPage}"
                        Margin="32"/>
      </ScrollViewer>

    </DockPanel>

  </Grid>
</Window>
```

---

## Dashboard Page

```xml
<!-- Views/DashboardView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:YourApp.ViewModels"
             x:DataType="vm:DashboardViewModel"
             x:Class="YourApp.Views.DashboardView">
  <StackPanel Spacing="{DynamicResource SpacingXXL}">

    <!-- Page header -->
    <StackPanel Spacing="4">
      <TextBlock Text="Dashboard"
                 FontSize="{DynamicResource FontSizeHeading}"
                 FontWeight="SemiBold"
                 Foreground="{DynamicResource BrushContentPrimary}"/>
      <TextBlock Text="Welcome back. Here's what's happening today."
                 FontSize="{DynamicResource FontSizeBody}"
                 Foreground="{DynamicResource BrushContentSecondary}"/>
    </StackPanel>

    <!-- KPI cards row -->
    <UniformGrid Columns="4" Gap="16">
      <!-- Metric card template — repeat for each KPI -->
      <Border Background="{DynamicResource BrushSurfaceCard}"
              BorderBrush="{DynamicResource BrushSurfaceBorder}"
              BorderThickness="1" CornerRadius="{DynamicResource RadiusL}"
              Padding="20">
        <StackPanel Spacing="4">
          <TextBlock Text="TOTAL REVENUE"
                     FontSize="{DynamicResource FontSizeSmall}"
                     FontWeight="Medium"
                     Foreground="{DynamicResource BrushContentSecondary}"/>
          <TextBlock Text="{Binding TotalRevenue}"
                     FontSize="28" FontWeight="Bold"
                     Foreground="{DynamicResource BrushContentPrimary}"/>
          <TextBlock Text="+8.2% vs last month"
                     FontSize="{DynamicResource FontSizeSmall}"
                     Foreground="#2E7D32"/>
        </StackPanel>
      </Border>
    </UniformGrid>

    <!-- Recent activity table -->
    <StackPanel Spacing="{DynamicResource SpacingL}">
      <Grid ColumnDefinitions="*,Auto">
        <TextBlock Text="Recent Activity"
                   FontSize="{DynamicResource FontSizeSubtitle}"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource BrushContentPrimary}"/>
        <Button Grid.Column="1" Content="View all"
                Theme="{StaticResource GhostButton}"
                FontSize="{DynamicResource FontSizeBody}"/>
      </Grid>

      <DataGrid ItemsSource="{Binding RecentItems}"
                AutoGenerateColumns="False"
                IsReadOnly="True"
                GridLinesVisibility="Horizontal"
                BorderThickness="1"
                BorderBrush="{DynamicResource BrushSurfaceBorder}"
                CornerRadius="{DynamicResource RadiusL}"
                Background="{DynamicResource BrushSurfaceCard}">
        <DataGrid.Columns>
          <DataGridTextColumn Header="Name"   Binding="{Binding Name}"   Width="*"/>
          <DataGridTextColumn Header="Type"   Binding="{Binding Type}"   Width="120"/>
          <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="120"/>
          <DataGridTextColumn Header="Date"   Binding="{Binding Date}"   Width="140"/>
        </DataGrid.Columns>
      </DataGrid>
    </StackPanel>

  </StackPanel>
</UserControl>
```

---

## Settings Page

```xml
<UserControl ...>
  <StackPanel Spacing="{DynamicResource SpacingXXL}" MaxWidth="720">

    <!-- Page header -->
    <StackPanel Spacing="4">
      <TextBlock Text="Settings" FontSize="{DynamicResource FontSizeHeading}"
                 FontWeight="SemiBold" Foreground="{DynamicResource BrushContentPrimary}"/>
      <TextBlock Text="Manage your preferences and account settings."
                 FontSize="{DynamicResource FontSizeBody}"
                 Foreground="{DynamicResource BrushContentSecondary}"/>
    </StackPanel>

    <!-- Section: Profile -->
    <Border Background="{DynamicResource BrushSurfaceCard}"
            BorderBrush="{DynamicResource BrushSurfaceBorder}"
            BorderThickness="1" CornerRadius="{DynamicResource RadiusL}">
      <StackPanel>

        <!-- Section header -->
        <Border Padding="20,16" BorderBrush="{DynamicResource BrushSurfaceBorder}" BorderThickness="0,0,0,1">
          <TextBlock Text="Profile" FontSize="{DynamicResource FontSizeSubtitle}"
                     FontWeight="SemiBold" Foreground="{DynamicResource BrushContentPrimary}"/>
        </Border>

        <!-- Fields -->
        <StackPanel Spacing="{DynamicResource SpacingL}" Padding="20">
          <Grid ColumnDefinitions="*,*" ColumnSpacing="16">
            <StackPanel Grid.Column="0" Spacing="4">
              <TextBlock Text="First Name" FontSize="{DynamicResource FontSizeBody}"
                         FontWeight="Medium" Foreground="{DynamicResource BrushContentSecondary}"/>
              <TextBox Theme="{StaticResource DefaultTextBox}" Text="{Binding FirstName}"/>
            </StackPanel>
            <StackPanel Grid.Column="1" Spacing="4">
              <TextBlock Text="Last Name" FontSize="{DynamicResource FontSizeBody}"
                         FontWeight="Medium" Foreground="{DynamicResource BrushContentSecondary}"/>
              <TextBox Theme="{StaticResource DefaultTextBox}" Text="{Binding LastName}"/>
            </StackPanel>
          </Grid>

          <StackPanel Spacing="4">
            <TextBlock Text="Email" FontSize="{DynamicResource FontSizeBody}"
                       FontWeight="Medium" Foreground="{DynamicResource BrushContentSecondary}"/>
            <TextBox Theme="{StaticResource DefaultTextBox}" Text="{Binding Email}"/>
          </StackPanel>
        </StackPanel>

        <!-- Footer actions -->
        <Border Padding="20,12" BorderBrush="{DynamicResource BrushSurfaceBorder}" BorderThickness="0,1,0,0">
          <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <Button Content="Discard" Theme="{StaticResource GhostButton}"/>
            <Button Content="Save changes" Theme="{StaticResource PrimaryButton}"/>
          </StackPanel>
        </Border>

      </StackPanel>
    </Border>

    <!-- Section: Appearance -->
    <Border Background="{DynamicResource BrushSurfaceCard}"
            BorderBrush="{DynamicResource BrushSurfaceBorder}"
            BorderThickness="1" CornerRadius="{DynamicResource RadiusL}">
      <StackPanel>
        <Border Padding="20,16" BorderBrush="{DynamicResource BrushSurfaceBorder}" BorderThickness="0,0,0,1">
          <TextBlock Text="Appearance" FontSize="{DynamicResource FontSizeSubtitle}"
                     FontWeight="SemiBold" Foreground="{DynamicResource BrushContentPrimary}"/>
        </Border>
        <Grid ColumnDefinitions="*,Auto" Padding="20,16">
          <StackPanel Grid.Column="0" Spacing="2">
            <TextBlock Text="Dark mode" FontSize="{DynamicResource FontSizeBody}"
                       FontWeight="Medium" Foreground="{DynamicResource BrushContentPrimary}"/>
            <TextBlock Text="Switch between light and dark interface"
                       FontSize="{DynamicResource FontSizeSmall}"
                       Foreground="{DynamicResource BrushContentSecondary}"/>
          </StackPanel>
          <ToggleSwitch Grid.Column="1" IsChecked="{Binding IsDarkMode}" VerticalAlignment="Center"/>
        </Grid>
      </StackPanel>
    </Border>

    <!-- Danger zone -->
    <Border BorderBrush="#FFCDD2" BorderThickness="1"
            CornerRadius="{DynamicResource RadiusL}">
      <StackPanel>
        <Border Padding="20,16" BorderBrush="#FFCDD2" BorderThickness="0,0,0,1"
                Background="#FFF8F8">
          <TextBlock Text="Danger Zone" FontSize="{DynamicResource FontSizeSubtitle}"
                     FontWeight="SemiBold" Foreground="#C62828"/>
        </Border>
        <Grid ColumnDefinitions="*,Auto" Padding="20,16">
          <StackPanel Grid.Column="0" Spacing="2">
            <TextBlock Text="Delete account" FontSize="{DynamicResource FontSizeBody}"
                       FontWeight="Medium" Foreground="{DynamicResource BrushContentPrimary}"/>
            <TextBlock Text="Permanently remove your account and all associated data."
                       FontSize="{DynamicResource FontSizeSmall}"
                       Foreground="{DynamicResource BrushContentSecondary}"/>
          </StackPanel>
          <Button Grid.Column="1" Content="Delete account"
                  Theme="{StaticResource DangerButton}" VerticalAlignment="Center"/>
        </Grid>
      </StackPanel>
    </Border>

  </StackPanel>
</UserControl>
```

---

## List / Detail Split View

```xml
<Grid ColumnDefinitions="320,*">

  <!-- List pane -->
  <Border Grid.Column="0"
          BorderBrush="{DynamicResource BrushSurfaceBorder}"
          BorderThickness="0,0,1,0"
          Background="{DynamicResource BrushSurfaceCard}">
    <DockPanel>
      <!-- List header -->
      <Border DockPanel.Dock="Top" Padding="16,12"
              BorderBrush="{DynamicResource BrushSurfaceBorder}" BorderThickness="0,0,0,1">
        <Grid ColumnDefinitions="*,Auto">
          <TextBlock Text="Items" FontSize="{DynamicResource FontSizeSubtitle}"
                     FontWeight="SemiBold" VerticalAlignment="Center"
                     Foreground="{DynamicResource BrushContentPrimary}"/>
          <Button Grid.Column="1" Content="+ New"
                  Theme="{StaticResource PrimaryButton}" Padding="10,6"/>
        </Grid>
      </Border>
      <!-- List -->
      <ListBox ItemsSource="{Binding Items}" SelectedItem="{Binding SelectedItem}"
               Background="Transparent" Margin="8">
        <ListBox.ItemTemplate>
          <DataTemplate>
            <Border Padding="12,10" CornerRadius="{DynamicResource RadiusM}">
              <StackPanel Spacing="2">
                <TextBlock Text="{Binding Name}" FontWeight="Medium"
                           Foreground="{DynamicResource BrushContentPrimary}"/>
                <TextBlock Text="{Binding Subtitle}"
                           FontSize="{DynamicResource FontSizeSmall}"
                           Foreground="{DynamicResource BrushContentSecondary}"/>
              </StackPanel>
            </Border>
          </DataTemplate>
        </ListBox.ItemTemplate>
      </ListBox>
    </DockPanel>
  </Border>

  <!-- Detail pane -->
  <ScrollViewer Grid.Column="1" Padding="32">
    <ContentControl Content="{Binding SelectedItem}"/>
  </ScrollViewer>

</Grid>
```

---

## Login / Auth Page

```xml
<Grid Background="{DynamicResource BrushSurfaceBackground}">
  <Border Width="400" HorizontalAlignment="Center" VerticalAlignment="Center"
          Background="{DynamicResource BrushSurfaceCard}"
          BorderBrush="{DynamicResource BrushSurfaceBorder}"
          BorderThickness="1"
          CornerRadius="{DynamicResource RadiusXL}"
          Padding="40">
    <StackPanel Spacing="{DynamicResource SpacingXL}">

      <!-- Logo / brand -->
      <StackPanel HorizontalAlignment="Center" Spacing="8">
        <Border Width="40" Height="40" CornerRadius="10"
                Background="{DynamicResource BrushAccentPrimary}"
                HorizontalAlignment="Center"/>
        <TextBlock Text="AppName" FontSize="{DynamicResource FontSizeTitle}"
                   FontWeight="Bold" HorizontalAlignment="Center"
                   Foreground="{DynamicResource BrushContentPrimary}"/>
      </StackPanel>

      <!-- Form -->
      <StackPanel Spacing="{DynamicResource SpacingL}">
        <StackPanel Spacing="4">
          <TextBlock Text="Email" FontSize="{DynamicResource FontSizeBody}"
                     FontWeight="Medium" Foreground="{DynamicResource BrushContentSecondary}"/>
          <TextBox Theme="{StaticResource DefaultTextBox}"
                   Watermark="name@company.com" Text="{Binding Email}"/>
        </StackPanel>
        <StackPanel Spacing="4">
          <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="Password" FontSize="{DynamicResource FontSizeBody}"
                       FontWeight="Medium" Foreground="{DynamicResource BrushContentSecondary}"/>
            <TextBlock Grid.Column="1" Text="Forgot password?"
                       FontSize="{DynamicResource FontSizeBody}"
                       Foreground="{DynamicResource BrushAccentPrimary}"
                       Cursor="Hand"/>
          </Grid>
          <TextBox Theme="{StaticResource DefaultTextBox}"
                   PasswordChar="*" Watermark="••••••••" Text="{Binding Password}"/>
        </StackPanel>
      </StackPanel>

      <!-- Sign in button -->
      <Button Content="Sign in" Theme="{StaticResource PrimaryButton}"
              HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
              Command="{Binding SignInCommand}"/>

      <!-- Footer -->
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Spacing="4">
        <TextBlock Text="Don't have an account?"
                   FontSize="{DynamicResource FontSizeBody}"
                   Foreground="{DynamicResource BrushContentSecondary}"/>
        <TextBlock Text="Sign up"
                   FontSize="{DynamicResource FontSizeBody}"
                   Foreground="{DynamicResource BrushAccentPrimary}"
                   Cursor="Hand"/>
      </StackPanel>

    </StackPanel>
  </Border>
</Grid>
```
