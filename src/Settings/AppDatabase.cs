namespace L2Toolkit.Settings
{
    public static class AppDatabase
    {
        private static Database? _database;

        public static Database GetInstance()
        {
            return _database ??= new Database();
        }
    }
}