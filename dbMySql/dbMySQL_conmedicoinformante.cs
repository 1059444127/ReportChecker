using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;

// ESTA VERSION ES LA QUE DEJO DE FUNCIONAR EL 29 de NOVIEMBRE de 2016
// LUEGO DE QUE JAVIER RUIZ BORRARA LA TABLA tu_med DEL SQL.

namespace dbMySql_1 {
    class dbMySql {
        public string serverAddress;
        public string serverPort;
        public string databaseName;
        public string databaseUsername;
        public string databasePassword;
        public bool limitResults;
        public int limitResultsTo;
        public bool useWildcard;

        private MySqlConnection connection;

        public dbMySql() {
        }

        ~dbMySql() {
            Disconnect();
        }

        private void Disconnect() {
            if (connection.State != ConnectionState.Closed) connection.Close();
        }

        public void CreateConnection() {
            if (connection == null) {
                string connectionString =
                    "SERVER=" + serverAddress + ";" +
                    "PORT=" + serverPort + ";" +
                    "DATABASE=" + databaseName + ";" +
                    "UID=" + databaseUsername + ";" +
                    "PWD=" + databasePassword + ";" +
                    "default command timeout = 30;";
                connection = new MySqlConnection();
                connection.ConnectionString = connectionString;
            }
        }

        public void TestConnection() {
            if (connection.State != ConnectionState.Closed) connection.Close();
            connection.Open();
            if (connection.State != ConnectionState.Closed) connection.Close();
        }

        public DataTable SelectStudyByAccessionNumber(long AccessionNumber) {
            var query = string.Format(
                            "SELECT " +
                            //"concat(t.fecha, \" \" , LEFT(t.hora, 2), \":\", RIGHT(t.hora, 2), \":00.000\") AS Fecha, " +
                            "concat(t.fecha, \" \" , LEFT(t.hora, 2), \":\", RIGHT(t.hora, 2)) AS Fecha, " +
                            "t.practica as 'N° Acc.', " +
                            "t.dni AS DNI, " +
                            "t.paciente AS Paciente, " +
                            "t.nacio AS Nacimiento, " +
                            "m.nombre AS Medico, " +
                            "t.med_solic AS 'ID Medico referente', " +
                            "t.nmed_solic AS 'Medico referente', " +
                            "t.equipo as Equipo, " +
                            // "t.peso AS Peso, " +
                            // "t.altura AS Altura, " +
                            "t.estudios as Estudios, " +
                            "t.estudio2 as Estudio2, " +
                            // "t.hora_llega as Recepcion, " +
                            // "t.hora_ingre as Ingreso, " + 
                            "t.localidad as Localidad, " +
                            "n.nombre AS 'Medico informante' " +
                            "FROM ambulatorio.turno as t " +
                            "INNER JOIN ambulatorio.tu_med as m " +
                            "ON t.matricula = m.matricula " +
                            "LEFT OUTER JOIN ambulatorio.tu_med as n " +
                            "ON t.matricula4 = n.matricula " +
                            "WHERE t.practica = {0};", AccessionNumber);


            var returnTable = new DataTable();
            var cmd = new MySqlCommand(query, connection);
            var da = new MySqlDataAdapter(cmd);
            da.Fill(returnTable);
            return returnTable;
        }

        public int CountStudiesByName(string name) {
            var queryCount = "SELECT COUNT(*) FROM ambulatorio.turno WHERE turno.paciente LIKE \'";
            if (useWildcard) {
                queryCount += name.Replace('*', '%');
            } else {
                queryCount += name + "%";
            }
            queryCount += "\';";

            connection.Open();
            var cmd = new MySqlCommand(queryCount, connection);
            object resultado = int.Parse(cmd.ExecuteScalar().ToString());
            int result = (int)resultado;
            connection.Close();
            return result;
        }

        public DataTable SelectStudyByName(string name) {


            var query = string.Format(
                            "SELECT " +
                            //"concat(t.fecha, \" \" , LEFT(t.hora, 2), \":\", RIGHT(t.hora, 2), \":00.000\") AS Fecha, " +
                            "concat(t.fecha, \" \" , LEFT(t.hora, 2), \":\", RIGHT(t.hora, 2)) AS Fecha, " +
                            "t.practica as 'N° Acc.', " +
                            "t.dni AS DNI, " +
                            "t.paciente AS Paciente, " +
                            "t.nacio AS Nacimiento, " +
                            "m.nombre AS Medico, " +
                            "t.med_solic AS 'ID Medico referente', " +
                            "t.nmed_solic AS 'Medico referente', " +
                            "t.equipo as Equipo, " +
                            // "t.peso AS Peso, " +
                            // "t.altura AS Altura, " +
                            "t.estudios as Estudios, " +
                            "t.estudio2 as Estudio2, " +
                            // "t.hora_llega as Recepcion, " +
                            // "t.hora_ingre as Ingreso, " + 
                            "t.localidad as Localidad, " +
                            "n.nombre AS 'Medico informante' " +
                            "FROM ambulatorio.turno as t " +
                            "INNER JOIN ambulatorio.tu_med as m " +
                            "ON t.matricula = m.matricula " +
                            "LEFT OUTER JOIN ambulatorio.tu_med as n " +
                            "ON t.matricula4 = n.matricula " +
                            // Just one kind of like, cause %{0}% would be EXTREMELY
                            // slow cause worst (not wrong) worst designed database.
                            "WHERE t.paciente LIKE \'");
            if (useWildcard) {
                query += name.Replace('*', '%');
            } else {
                query += name + "%";
            }

            query += "\'";

            if (limitResults && limitResultsTo > 0) {
                query += string.Format(" LIMIT {0}", limitResultsTo);
            }

            query += ";";

            DataTable returnTableByName = new DataTable();
            var cmd = new MySqlCommand(query, connection);
            var da = new MySqlDataAdapter(cmd);

            da.Fill(returnTableByName);
            return returnTableByName;
        }

        //struct bgwObj {
        //    public MySqlConnection connection;
        //    public MySqlCommand cmd;
        //    public DataTable returnTable;
        //}



        public DataTable SelectStudyByID(int ID) {
            var query = string.Format(
                            "SELECT " +
                            //"concat(t.fecha, \" \" , LEFT(t.hora, 2), \":\", RIGHT(t.hora, 2), \":00.000\") AS Fecha, " +
                            "concat(t.fecha, \" \" , LEFT(t.hora, 2), \":\", RIGHT(t.hora, 2)) AS Fecha, " +
                            "t.practica as 'N° Acc.', " +
                            "t.dni AS DNI, " +
                            "t.paciente AS Paciente, " +
                            "t.nacio AS Nacimiento, " +
                            "m.nombre AS Medico, " +
                            "t.med_solic AS 'ID Medico referente', " +
                            "t.nmed_solic AS 'Medico referente', " +
                            "t.equipo as Equipo, " +
                            // "t.peso AS Peso, " +
                            // "t.altura AS Altura, " +
                            "t.estudios as Estudios, " +
                            "t.estudio2 as Estudio2, " +
                            // "t.hora_llega as Recepcion, " +
                            // "t.hora_ingre as Ingreso, " + 
                            "t.localidad as Localidad, " +
                            "n.nombre AS 'Medico informante' " +
                            "FROM ambulatorio.turno as t " +
                            "INNER JOIN ambulatorio.tu_med as m " +
                            "ON t.matricula = m.matricula " +
                            "LEFT OUTER JOIN ambulatorio.tu_med as n " +
                            "ON t.matricula4 = n.matricula " +
                            "WHERE t.dni = " + ID);

            if (limitResults && limitResultsTo > 0) {
                query += string.Format(" LIMIT {0}", limitResultsTo);
            }

            query += ";";

            var returnTable = new DataTable();
            var cmd = new MySqlCommand(query, connection);
            var da = new MySqlDataAdapter(cmd);
            da.Fill(returnTable);
            return returnTable;
        }
    }
}
