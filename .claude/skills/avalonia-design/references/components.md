# Component Library — AXAML

All components use `DynamicResource` for colors. Define tokens first (see `design-tokens.md`).

---

## Buttons

### Primary Button
```xml
<ControlTheme x:Key="PrimaryButton" TargetType="Button">
  <Setter Property="Background"    Value="{DynamicResource BrushAccentPrimary}"/>
  <Setter Property="Foreground"    Value="{DynamicResource BrushAccentForeground}"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="CornerRadius"  Value="{DynamicResource RadiusM}"/>
  <Setter Property="Padding"       Value="16,9"/>
  <Setter Property="FontSize"      Value="{DynamicResource FontSizeBody}"/>
  <Setter Property="FontWeight"    Value="Medium"/>
  <Setter Property="Cursor"        Value="Hand"/>
  <Setter Property="Transitions">
    <Transitions>
      <BrushTransition Property="Background" Duration="0:0:0.12"/>
    </Transitions>
  </Setter>
  <Style Selector="^:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BrushAccentHover}"/>
  </Style>
  <Style Selector="^:pressed /template/ ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BrushAccentPressed}"/>
    <Setter Property="RenderTransform" Value="scale(0.98)"/>
  </Style>
  <Style Selector="^:disabled">
    <Setter Property="Opacity" Value="0.4"/>
    <Setter Property="Cursor"  Value="Arrow"/>
  </Style>
</ControlTheme>
```

### Secondary (Outlined) Button
```xml
<ControlTheme x:Key="SecondaryButton" TargetType="Button">
  <Setter Property="Background"      Value="Transparent"/>
  <Setter Property="Foreground"      Value="{DynamicResource BrushAccentPrimary}"/>
  <Setter Property="BorderBrush"     Value="{DynamicResource BrushAccentPrimary}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{DynamicResource RadiusM}"/>
  <Setter Property="Padding"         Value="16,8"/>
  <Setter Property="FontSize"        Value="{DynamicResource FontSizeBody}"/>
  <Setter Property="Cursor"          Value="Hand"/>
  <Style Selector="^:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BrushAccentSubtle}"/>
  </Style>
  <Style Selector="^:disabled">
    <Setter Property="Opacity" Value="0.4"/>
  </Style>
</ControlTheme>
```

### Ghost Button
```xml
<ControlTheme x:Key="GhostButton" TargetType="Button">
  <Setter Property="Background"      Value="Transparent"/>
  <Setter Property="Foreground"      Value="{DynamicResource BrushContentSecondary}"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="CornerRadius"    Value="{DynamicResource RadiusM}"/>
  <Setter Property="Padding"         Value="12,8"/>
  <Setter Property="FontSize"        Value="{DynamicResource FontSizeBody}"/>
  <Setter Property="Cursor"          Value="Hand"/>
  <Style Selector="^:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BrushSurfaceBorder}"/>
    <Setter Property="TextElement.Foreground" Value="{DynamicResource BrushContentPrimary}"/>
  </Style>
</ControlTheme>
```

### Danger Button
```xml
<ControlTheme x:Key="DangerButton" TargetType="Button">
  <Setter Property="Background"    Value="#C62828"/>
  <Setter Property="Foreground"    Value="#FFFFFF"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="CornerRadius"  Value="{DynamicResource RadiusM}"/>
  <Setter Property="Padding"       Value="16,9"/>
  <Setter Property="Cursor"        Value="Hand"/>
  <Style Selector="^:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#B71C1C"/>
  </Style>
</ControlTheme>
```

---

## Text Input

```xml
<ControlTheme x:Key="DefaultTextBox" TargetType="TextBox">
  <Setter Property="Background"      Value="{DynamicResource BrushSurfaceCard}"/>
  <Setter Property="Foreground"      Value="{DynamicResource BrushContentPrimary}"/>
  <Setter Property="BorderBrush"     Value="{DynamicResource BrushSurfaceBorder}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{DynamicResource RadiusS}"/>
  <Setter Property="Padding"         Value="12,9"/>
  <Setter Property="FontSize"        Value="{DynamicResource FontSizeBody}"/>
  <Setter Property="MinHeight"       Value="36"/>
  <Style Selector="^:focus">
    <Setter Property="BorderBrush" Value="{DynamicResource BrushAccentPrimary}"/>
  </Style>
  <Style Selector="^:disabled">
    <Setter Property="Opacity" Value="0.5"/>
  </Style>
</ControlTheme>
```

Usage:
```xml
<StackPanel Spacing="4">
  <TextBlock Text="Email" FontSize="{DynamicResource FontSizeBody}"
             Foreground="{DynamicResource BrushContentSecondary}"/>
  <TextBox Theme="{StaticResource DefaultTextBox}"
           Watermark="name@company.com"/>
</StackPanel>
```

---

## Search Box

```xml
<Border Background="{DynamicResource BrushSurfaceCard}"
        BorderBrush="{DynamicResource BrushSurfaceBorder}"
        BorderThickness="1"
        CornerRadius="{DynamicResource RadiusS}"
        Padding="10,0">
  <Grid ColumnDefinitions="Auto,*">
    <PathIcon Grid.Column="0" Width="14" Height="14"
              Foreground="{DynamicResource BrushContentSecondary}"
              Data="M21 21L16.514 16.506M19 10.5C19 15.194 15.194 19 10.5 19C5.806 19 2 15.194 2 10.5C2 5.806 5.806 2 10.5 2C15.194 2 19 5.806 19 10.5Z"
              Margin="0,0,8,0"/>
    <TextBox Grid.Column="1" BorderThickness="0" Background="Transparent"
             Watermark="Search..." Padding="0,9"/>
  </Grid>
</Border>
```

---

## ComboBox / Dropdown

```xml
<ControlTheme x:Key="DefaultComboBox" TargetType="ComboBox">
  <Setter Property="Background"      Value="{DynamicResource BrushSurfaceCard}"/>
  <Setter Property="Foreground"      Value="{DynamicResource BrushContentPrimary}"/>
  <Setter Property="BorderBrush"     Value="{DynamicResource BrushSurfaceBorder}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{DynamicResource RadiusS}"/>
  <Setter Property="Padding"         Value="12,8"/>
  <Setter Property="MinHeight"       Value="36"/>
  <Style Selector="^:focus">
    <Setter Property="BorderBrush" Value="{DynamicResource BrushAccentPrimary}"/>
  </Style>
</ControlTheme>
```

---

## Checkbox

```xml
<ControlTheme x:Key="DefaultCheckBox" TargetType="CheckBox">
  <Setter Property="Foreground" Value="{DynamicResource BrushContentPrimary}"/>
  <Setter Property="FontSize"   Value="{DynamicResource FontSizeBody}"/>
  <!-- Avalonia's default checkbox is used; override box color via template if needed -->
</ControlTheme>
```

For a custom checkbox box background when checked:
```xml
<Style Selector="CheckBox:checked /template/ Border#NormalRectangle">
  <Setter Property="Background" Value="{DynamicResource BrushAccentPrimary}"/>
  <Setter Property="BorderBrush" Value="{DynamicResource BrushAccentPrimary}"/>
</Style>
```

---

## Toggle Switch

```xml
<ToggleSwitch IsChecked="{Binding IsEnabled}"
              OnContent="On" OffContent="Off"
              Foreground="{DynamicResource BrushContentPrimary}"/>
```

Color the track when on via style:
```xml
<Style Selector="ToggleSwitch:checked /template/ Border#SwitchKnobBounds">
  <Setter Property="Background" Value="{DynamicResource BrushAccentPrimary}"/>
</Style>
```

---

## Card / Panel

```xml
<!-- Standard card -->
<Border Background="{DynamicResource BrushSurfaceCard}"
        BorderBrush="{DynamicResource BrushSurfaceBorder}"
        BorderThickness="1"
        CornerRadius="{DynamicResource RadiusL}"
        Padding="{DynamicResource SpacingL}">
  <StackPanel Spacing="{DynamicResource SpacingM}">
    <!-- content -->
  </StackPanel>
</Border>
```

```xml
<!-- Metric / KPI card -->
<Border Background="{DynamicResource BrushSurfaceCard}"
        BorderBrush="{DynamicResource BrushSurfaceBorder}"
        BorderThickness="1"
        CornerRadius="{DynamicResource RadiusL}"
        Padding="20">
  <StackPanel Spacing="4">
    <TextBlock Text="Total Revenue"
               FontSize="{DynamicResource FontSizeSmall}"
               Foreground="{DynamicResource BrushContentSecondary}"
               FontWeight="Medium"
               TextTransform="Uppercase"
               LetterSpacing="0.5"/>
    <TextBlock Text="$124,580"
               FontSize="{DynamicResource FontSizeDisplay}"
               FontWeight="Bold"
               Foreground="{DynamicResource BrushContentPrimary}"/>
    <TextBlock Text="+8.2% vs last month"
               FontSize="{DynamicResource FontSizeSmall}"
               Foreground="#2E7D32"/>
  </StackPanel>
</Border>
```

---

## DataGrid / Table

```xml
<DataGrid ItemsSource="{Binding Items}"
          AutoGenerateColumns="False"
          IsReadOnly="True"
          GridLinesVisibility="Horizontal"
          BorderThickness="1"
          BorderBrush="{DynamicResource BrushSurfaceBorder}"
          CornerRadius="{DynamicResource RadiusL}"
          Background="{DynamicResource BrushSurfaceCard}">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Name"   Binding="{Binding Name}"   Width="*"/>
    <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="120"/>
    <DataGridTextColumn Header="Date"   Binding="{Binding Date}"   Width="140"/>
  </DataGrid.Columns>
</DataGrid>
```

Style header:
```xml
<Style Selector="DataGridColumnHeader">
  <Setter Property="Background"  Value="{DynamicResource BrushSurfaceBackground}"/>
  <Setter Property="Foreground"  Value="{DynamicResource BrushContentSecondary}"/>
  <Setter Property="FontSize"    Value="{DynamicResource FontSizeSmall}"/>
  <Setter Property="FontWeight"  Value="Medium"/>
  <Setter Property="Padding"     Value="16,10"/>
</Style>
<Style Selector="DataGridRow:pointerover /template/ Rectangle#BackgroundRectangle">
  <Setter Property="Fill"    Value="{DynamicResource BrushAccentSubtle}"/>
  <Setter Property="Opacity" Value="1"/>
</Style>
```

---

## Badge / Status Chip

```xml
<!-- Status badge (reusable) -->
<Border CornerRadius="{DynamicResource RadiusFull}"
        Padding="8,3"
        Background="#E8F5E9">
  <TextBlock Text="Active"
             FontSize="{DynamicResource FontSizeSmall}"
             FontWeight="Medium"
             Foreground="#2E7D32"/>
</Border>

<!-- Danger badge -->
<Border CornerRadius="{DynamicResource RadiusFull}"
        Padding="8,3"
        Background="#FFEBEE">
  <TextBlock Text="Inactive"
             FontSize="{DynamicResource FontSizeSmall}"
             FontWeight="Medium"
             Foreground="#C62828"/>
</Border>
```

---

## Sidebar Navigation Item

```xml
<ControlTheme x:Key="NavItem" TargetType="Button">
  <Setter Property="Background"      Value="Transparent"/>
  <Setter Property="Foreground"      Value="{DynamicResource BrushContentSecondary}"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="CornerRadius"    Value="{DynamicResource RadiusM}"/>
  <Setter Property="Padding"         Value="12,10"/>
  <Setter Property="HorizontalContentAlignment" Value="Left"/>
  <Setter Property="Cursor"          Value="Hand"/>
  <Style Selector="^:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BrushSurfaceBorder}"/>
    <Setter Property="TextElement.Foreground" Value="{DynamicResource BrushContentPrimary}"/>
  </Style>
</ControlTheme>

<!-- Active nav item — apply this style when route matches -->
<Style Selector="Button.NavItemActive">
  <Setter Property="Background" Value="{DynamicResource BrushAccentSubtle}"/>
  <Setter Property="Foreground" Value="{DynamicResource BrushAccentPrimary}"/>
  <Setter Property="FontWeight" Value="Medium"/>
</Style>
```

Usage:
```xml
<Button Theme="{StaticResource NavItem}" Classes="NavItemActive">
  <StackPanel Orientation="Horizontal" Spacing="10">
    <PathIcon Width="16" Height="16" Data="..."/>
    <TextBlock Text="Dashboard"/>
  </StackPanel>
</Button>
```

---

## Dialog / Modal

```xml
<!-- Shown inside a Popup or Window with WindowStartupLocation="CenterOwner" -->
<Border Background="{DynamicResource BrushSurfaceCard}"
        BorderBrush="{DynamicResource BrushSurfaceBorder}"
        BorderThickness="1"
        CornerRadius="{DynamicResource RadiusXL}"
        Padding="{DynamicResource SpacingXXL}"
        Width="480"
        BoxShadow="0 8 32 0 #18000000">
  <StackPanel Spacing="{DynamicResource SpacingL}">

    <!-- Header -->
    <StackPanel Spacing="6">
      <TextBlock Text="Confirm Action"
                 FontSize="{DynamicResource FontSizeTitle}"
                 FontWeight="SemiBold"
                 Foreground="{DynamicResource BrushContentPrimary}"/>
      <TextBlock Text="This will permanently delete the item. This action cannot be undone."
                 FontSize="{DynamicResource FontSizeBody}"
                 Foreground="{DynamicResource BrushContentSecondary}"
                 TextWrapping="Wrap"/>
    </StackPanel>

    <!-- Actions -->
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
      <Button Content="Cancel"  Theme="{StaticResource GhostButton}"/>
      <Button Content="Delete"  Theme="{StaticResource DangerButton}"/>
    </StackPanel>

  </StackPanel>
</Border>
```

---

## Progress Bar

```xml
<ProgressBar Value="{Binding Progress}"
             Minimum="0" Maximum="100"
             Height="6"
             CornerRadius="3"
             Background="{DynamicResource BrushSurfaceBorder}"
             Foreground="{DynamicResource BrushAccentPrimary}"/>
```

---

## Separator / Divider

```xml
<Separator Background="{DynamicResource BrushSurfaceBorder}" Height="1" Margin="0,8"/>
```

Or for section headers:
```xml
<Grid ColumnDefinitions="*,Auto,*" VerticalAlignment="Center" Margin="0,16">
  <Separator Grid.Column="0" Background="{DynamicResource BrushSurfaceBorder}" Height="1"/>
  <TextBlock Grid.Column="1" Text="OR" Margin="12,0"
             FontSize="{DynamicResource FontSizeSmall}"
             Foreground="{DynamicResource BrushContentTertiary}"/>
  <Separator Grid.Column="2" Background="{DynamicResource BrushSurfaceBorder}" Height="1"/>
</Grid>
```
