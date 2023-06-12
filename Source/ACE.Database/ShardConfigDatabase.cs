using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using ACE.Database.Models.Shard;

namespace ACE.Database
{
    public class ShardConfigDatabase
    {
        public virtual bool BoolExists(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesBoolean.Any(r => r.Key == key);
        }

        public virtual bool DoubleExists(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesDouble.Any(r => r.Key == key);
        }

        public virtual bool LongExists(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesLong.Any(r => r.Key == key);
        }

        public virtual bool StringExists(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesString.Any(r => r.Key == key);
        }


        public virtual void AddBool(string key, bool value, string description = null)
        {
            var stat = new ConfigPropertiesBoolean
            {
                Key = key,
                Value = value,
                Description = description
            };

            using (var context = new ShardDbContext())
            {
                context.ConfigPropertiesBoolean.Add(stat);

                context.SaveChanges();
            }
        }

        public virtual void AddLong(string key, long value, string description = null)
        {
            var stat = new ConfigPropertiesLong
            {
                Key = key,
                Value = value,
                Description = description
            };

            using (var context = new ShardDbContext())
            {
                context.ConfigPropertiesLong.Add(stat);

                context.SaveChanges();
            }
        }

        public virtual void AddDouble(string key, double value, string description = null)
        {
            var stat = new ConfigPropertiesDouble
            {
                Key = key,
                Value = value,
                Description = description
            };

            using (var context = new ShardDbContext())
            {
                context.ConfigPropertiesDouble.Add(stat);

                context.SaveChanges();
            }
        }

        public virtual void AddString(string key, string value, string description = null)
        {
            var stat = new ConfigPropertiesString
            {
                Key = key,
                Value = value,
                Description = description
            };

            using (var context = new ShardDbContext())
            {
                context.ConfigPropertiesString.Add(stat);

                context.SaveChanges();
            }
        }


        public virtual ConfigPropertiesBoolean GetBool(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesBoolean.AsNoTracking().FirstOrDefault(r => r.Key == key);
        }

        public virtual ConfigPropertiesLong GetLong(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesLong.AsNoTracking().FirstOrDefault(r => r.Key == key);
        }

        public virtual ConfigPropertiesDouble GetDouble(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesDouble.AsNoTracking().FirstOrDefault(r => r.Key == key);
        }

        public virtual ConfigPropertiesString GetString(string key)
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesString.AsNoTracking().FirstOrDefault(r => r.Key == key);
        }


        public virtual List<ConfigPropertiesBoolean> GetAllBools()
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesBoolean.AsNoTracking().ToList();
        }

        public virtual List<ConfigPropertiesLong> GetAllLongs()
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesLong.AsNoTracking().ToList();
        }

        public virtual List<ConfigPropertiesDouble> GetAllDoubles()
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesDouble.AsNoTracking().ToList();
        }

        public virtual List<ConfigPropertiesString> GetAllStrings()
        {
            using (var context = new ShardDbContext())
                return context.ConfigPropertiesString.AsNoTracking().ToList();
        }


        public virtual void SaveBool(ConfigPropertiesBoolean stat)
        {
            using (var context = new ShardDbContext())
            {
                context.Entry(stat).State = EntityState.Modified;

                context.SaveChanges();
            }
        }

        public virtual void SaveLong(ConfigPropertiesLong stat)
        {
            using (var context = new ShardDbContext())
            {
                context.Entry(stat).State = EntityState.Modified;

                context.SaveChanges();
            }
        }

        public virtual void SaveDouble(ConfigPropertiesDouble stat)
        {
            using (var context = new ShardDbContext())
            {
                context.Entry(stat).State = EntityState.Modified;

                context.SaveChanges();
            }
        }

        public virtual void SaveString(ConfigPropertiesString stat)
        {
            using (var context = new ShardDbContext())
            {
                context.Entry(stat).State = EntityState.Modified;

                context.SaveChanges();
            }
        }
    }
}
