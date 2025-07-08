namespace L2Toolkit.database
{
    public static class AppDatabase
    {
        private static Database _database;

        public static Database GetInstance()
        {
            return _database ??= new Database();
        }
    }
}