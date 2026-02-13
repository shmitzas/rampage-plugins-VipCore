using FluentMigrator;

namespace VIPCore.Database.Migrations;

[Migration(3)]
public class Migration003 : Migration
{
    public override void Up()
    {
        if (!Schema.Table("vip_users").Exists())
        {
            Create.Table("vip_users")
                .WithColumn("steam_id").AsInt64().NotNullable()
                .WithColumn("name").AsString(64).NotNullable()
                .WithColumn("last_visit").AsDateTime().NotNullable()
                .WithColumn("sid").AsInt64().NotNullable()
                .WithColumn("group").AsString(64).NotNullable()
                .WithColumn("expires").AsDateTime().NotNullable();

            Create.PrimaryKey("PK_vip_users").OnTable("vip_users").Columns("steam_id", "sid", "group");
        }
        else
        {
            if (Schema.Table("vip_users").Column("account_id").Exists())
            {
                Rename.Table("vip_users").To("vip_users_backup");

                Create.Table("vip_users")
                    .WithColumn("steam_id").AsInt64().NotNullable()
                    .WithColumn("name").AsString(64).NotNullable()
                    .WithColumn("last_visit").AsDateTime().NotNullable()
                    .WithColumn("sid").AsInt64().NotNullable()
                    .WithColumn("group").AsString(64).NotNullable()
                    .WithColumn("expires").AsDateTime().NotNullable();

                Create.PrimaryKey("PK_vip_users").OnTable("vip_users").Columns("steam_id", "sid", "group");

                Execute.Sql(@"
                    INSERT INTO vip_users (steam_id, name, last_visit, sid, `group`, expires)
                    SELECT 
                        account_id AS steam_id,
                        name,
                        FROM_UNIXTIME(lastvisit) AS last_visit,
                        sid,
                        `group`,
                        CASE 
                            WHEN expires = 0 THEN '0001-01-01 00:00:00'
                            ELSE FROM_UNIXTIME(expires)
                        END AS expires
                    FROM vip_users_backup
                ");
            }
        }
    }

    public override void Down()
    {
        if (Schema.Table("vip_users").Exists())
        {
            Rename.Table("vip_users").To("vip_users_backup");

            Create.Table("vip_users")
                .WithColumn("account_id").AsInt64().NotNullable()
                .WithColumn("name").AsString(64).NotNullable()
                .WithColumn("lastvisit").AsInt64().NotNullable()
                .WithColumn("sid").AsInt64().NotNullable()
                .WithColumn("group").AsString(64).NotNullable()
                .WithColumn("expires").AsInt64().NotNullable();

            Create.PrimaryKey("PK_vip_users").OnTable("vip_users").Columns("account_id", "sid", "group");

            Execute.Sql(@"
                INSERT INTO vip_users (account_id, name, lastvisit, sid, `group`, expires)
                SELECT 
                    steam_id AS account_id,
                    name,
                    UNIX_TIMESTAMP(last_visit) AS lastvisit,
                    sid,
                    `group`,
                    CASE 
                        WHEN expires = '0001-01-01 00:00:00' THEN 0
                        ELSE UNIX_TIMESTAMP(expires)
                    END AS expires
                FROM vip_users_backup
            ");
        }
    }
}
