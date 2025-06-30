namespace L2Toolkit.database
{
    public class AppDatabase
    {
        private static Database _database;

        public static Database GetInstance()
        {
            return _database ?? (_database = new Database());
        }
    }
}