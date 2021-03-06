using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;


namespace FireSim
{
    public enum Libres { MapadeBits, ListadeLibres, ListadeLibresdePrincipioyCuenta, Vacio = -1 };
    public enum Acceso { Secuencial, Directo, Indexado, Vacio = -1 };
    public enum Org { Contigua, Enlazada, Indexada, Vacio = -1 };

    public class FileSim
    {
        private Org organizacionFisica;
        private Libres adminEspacio;
        private Acceso metodoAcceso;
        private List<Operacion> TablaOperaciones;
        private List<int> ColaEspera;
        private int contColaEspera;
        private int opActual;
                                            //esta lista almacenaria las posiciones en la TablaOperaciones de las operaciones que 
                                            // no se pudieron realizar todavia (por ejemplo una operacion esta esperando que se cierre un archivo)
                                             
        private Dispositivo disp;
        private int ContadorOp;
        private List<Archivo> TablaArchivos;
        //c/ string NombreArchivo se tiene asociado una estructura Indicadores que almacena los resultados de la simulacion
        private List<Indicadores> indicadoresOP;
        private int tSimulacion;
       
        public FileSim(int tProc, Org orgFisica, Libres admEspacio, Acceso metAcceso,
                       int tLectura, int tEscritura, int tSeek, int tAcceso, int tamBloques, int tamDispositivo, string ruta)
        {
            //Crea las listas de operaciones, tabla de archivos y la cola de espera (vacias!)
            this.TablaOperaciones = new List<Operacion>();
            this.TablaArchivos = new List<Archivo>();
            this.ColaEspera = new List<int>();
            this.SetContadorOp(0);
            this.contColaEspera = 0;
            this.opActual = 0;

            // Por cada operacion se agrega un objeto Indicadores a la lista
            this.indicadoresOP = new List<Indicadores>();

            //Se crea el dispositivo --> se le pasan los parametros configurables relacionados con disp
            this.disp = new Dispositivo(tLectura, tEscritura, tSeek, tAcceso, tamBloques, tamDispositivo, tProc);

            //setters parametros fileSim
            SetOrganizacionFisica(orgFisica);
            SetAdminEspacio(admEspacio);
            SetMetodoAcceso(metAcceso);

            //Carga las operaciones desde el archivo ingresado x usuario --> las almacena en la tabla operaciones
            CargarOperaciones(ruta);

            this.tSimulacion = 0;

        }

        private void CargarOperaciones(string ruta)
        {
            // Lee el archivo, se cargan las operaciones en this.TablaOperaciones y se ordena por tArribo
            
            try
            {
                // El metodo ReadAllLines de File, cierra el archivo automaticamente.
                string[] lineas = File.ReadAllLines(ruta);
            
                Operacion opAux = new Operacion();
                foreach (var linea in lineas)
                {
                    var valores = linea.Split(';');
                    opAux.NombreArchivo = valores[0];
                    opAux.IdOperacion = valores[1];
                    switch (opAux.IdOperacion.ToString())
                    {
                        case "N": //N de new
                            opAux.IdOperacion = "CREATE";
                            break;
                        case "C": //C de close
                            opAux.IdOperacion = "CLOSE";
                            break;
                        case "O": //O de open
                            opAux.IdOperacion = "OPEN";
                            break;
                        case "D": //D de delete
                            opAux.IdOperacion = "DELETE";
                            break;
                        case "W": //W de write 
                            opAux.IdOperacion = "WRITE";
                            break;
                        case "R": //R de read
                            opAux.IdOperacion = "READ";
                            break;
                    }
                    opAux.NumProceso = Int32.Parse(valores[2]);
                    opAux.Tarribo = Int32.Parse(valores[3]);
                    opAux.Offset = Int32.Parse(valores[4]);
                    opAux.CantidadUA = Int32.Parse(valores[5]);
                    TablaOperaciones.Add(opAux);
                }

                TablaOperaciones.Sort(new ComparaOp());
                
                /*  Bloque solo de testeo de metodo Sort*/
                foreach(Operacion op in TablaOperaciones)
                {
                    Console.WriteLine(op.NombreArchivo + " " + op.IdOperacion + " " + op.NumProceso + " " +
                        op.Tarribo + " " + op.Offset + " " + op.CantidadUA);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e);
            }

        }

        public int CantBloques()
        {
            return this.disp.GetCantBloques();
        }

        // EstadoBloque devuelve un float de 0 a 1 indicando el estado del bloque
        // si es 0 el bloque esta vacio, si es 1 esta lleno, lo demas es el porcentaje de ocupacion del bloque
        public float EstadoBloque(int bloque) 
        {
            return disp.estadoBloque(bloque);
        }

        public void SimularSiguienteOp()
        {
            if (GetContadorOp() < GetCantidadOp())
            {
                SimularOp(GetContadorOp());
                SetContadorOp(GetContadorOp() + 1);
            }
            else if (contColaEspera < ColaEspera.Count)
            {
                SimularOp(contColaEspera);
                contColaEspera++;
            }
            opActual++;
        }


        public void SimularOp(int op)
        {
            Operacion nextOp = TablaOperaciones[op];
            // Todos los metodos deben devolver el tiempo que tardo en ejecutarse la operacion
            switch (nextOp.IdOperacion)
            {
                case "CREATE":
                    {
                        tSimulacion += Create(nextOp.NumProceso, nextOp.CantidadUA, nextOp.NombreArchivo);
                        break;
                    }
                case "DELETE":
                    {
                        tSimulacion += Delete(nextOp.NombreArchivo);
                        break;
                    }
                case "OPEN":
                    {
                        tSimulacion += Open(nextOp.NombreArchivo, nextOp.NumProceso);
                        break;
                    }
                case "CLOSE":
                    {
                        tSimulacion += Close(nextOp.NombreArchivo, nextOp.NumProceso);
                        break;
                    }
                case "READ":
                    {
                        try
                        {
                            tSimulacion += Read(nextOp.NombreArchivo, nextOp.NumProceso, nextOp.Offset, nextOp.CantidadUA);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error: " + e);
                        }
                        break;
                    }
                case "WRITE":
                    {
                        try
                        {
                            tSimulacion += Write(nextOp.NombreArchivo, nextOp.NumProceso, nextOp.Offset, nextOp.CantidadUA);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error: " + e);
                        }
                        
                        break;
                    }

                default:
                    {
                        Console.WriteLine("Operacion Incorrecta, se descarta");
                        break;
                    }
            }
            
        }
        public int Create(int idProc, int cant_uA, string name)
        {
            int tOP = 0;

            if (BuscaArch(name) == -1) //comprueba que el archivo no este creado ya
            {
                Archivo archivo = new Archivo(name, cant_uA);

                if (disp.GetLibres(cant_uA, GetOrganizacionFisica(), ref archivo))
                {
                    Indicadores indicador = new Indicadores();
                    TablaArchivos.Add(archivo); //agregamos el nuevo archivo a la tabla 
                    indicador.tGestionTotal = disp.TprocesamientoBloquesLibres(GetAdminEspacio(), cant_uA);
                    indicador.tLectoEscritura = 0;
                    indicador.tEspera = tSimulacion - TablaOperaciones[getOpActual()].Tarribo;
                    indicador.tipoOp = 'N';
                    indicador.tSatisfaccion = indicador.tLectoEscritura + indicador.tGestionTotal + indicador.tEspera;
                    tOP = indicador.tGestionTotal + indicador.tLectoEscritura;
                    indicadoresOP.Add(indicador);
                }
            }
            else //si el archivo ya esta creado
            {
                Console.WriteLine("Error: El archivo ya se encuentra creado!");
            }
            return tOP;
        }

        public int Write(string name, int idProc, int offset, int cant_uA)
        {
            int tOP = 0;
            int posArch = BuscaArch(name);
            
            if (posArch != -1) // Verifico que el archivo este creado
            {   
                if (TablaArchivos[posArch].getEstado() == idProc) // Verifico que el proceso que abrio el archivo sea el que lo quiere leer
                {
                    int inicio = (int)Math.Floor((decimal)(offset) / (decimal)(disp.GetTamBloques())); //bloque donde comienza a leerse
                    int fin = (int)Math.Ceiling((decimal)(offset + cant_uA) / (decimal)(disp.GetTamBloques()));
                    Indicadores indicador = new Indicadores();
                    Archivo arch = TablaArchivos[posArch]; //busco archivo en la tabla

                    if (fin > arch.getTablaDireccion().Count && GetOrganizacionFisica() == Org.Contigua) //solo se realoca si es contigua
                    {
                        if (!realocar(ref arch, fin, arch.getTablaDireccion().Count, ref indicador)) //PROBAR SI MODIFICA ARCH ASI!!!!!!!
                        {
                            throw new Exception("No se pudo realocar.");
                        }
                    }
                    else if (fin > arch.getTablaDireccion().Count) //para enlazada e indexada
                    {
                        indicador.tGestionTotal = disp.TprocesamientoBloquesLibres(GetAdminEspacio(), fin - arch.getTablaDireccion().Count); 
                    }

                    switch (GetOrganizacionFisica())
                    {
                        case Org.Contigua:
                        {
                                indicador.tGestionTotal += disp.GetTseek(); //el t de gestion es el t en ir al primer bloque
                                break;
                        }
                        case Org.Enlazada:
                        {
                                int bloqueLeido = -1;

                                for (int i = 0; i < fin; i++)
                                {
                                    if (arch.getTablaDireccion()[i] != bloqueLeido - 1) //comprueba si los bloques son contiguos
                                    {
                                        indicador.tGestionTotal += disp.GetTseek();
                                    }
                                    bloqueLeido = arch.getTablaDireccion()[i];
                                }

                                indicador.tGestionTotal += inicio * disp.GetTlectura();

                                break;
                        }
                        case Org.Indexada:
                        {
                                indicador.tGestionTotal += (2 * disp.GetTseek() + disp.GetTprocesamient()) * (fin - inicio);
                                break;
                        }
                    }

                    indicador.tEspera = tSimulacion - TablaOperaciones[getOpActual()].Tarribo;
                    indicador.tLectoEscritura = (fin - inicio) * disp.GetTescritura();
                    indicador.tSatisfaccion = indicador.tGestionTotal + indicador.tEspera + indicador.tLectoEscritura;
                    indicador.tipoOp = 'W';

                    tOP = indicador.tSatisfaccion - indicador.tEspera;
                    indicadoresOP.Add(indicador);
                }
                else
                {
                    if (!Find(ColaEspera, GetContadorOp()))
                    {
                        ColaEspera.Add(GetContadorOp()); // Si no es asi, lo agrego a la cola de espera
                    }
                }
            }

            
            return tOP;
        }

        public int Read(string name, int idProc, int offset, int cant_uA)
        {
            int tOP = 0;
            int posArch = BuscaArch(name);
         
            if (posArch != -1)   // Verifico que el archivo este creado
            {   
                if (TablaArchivos[posArch].getEstado() == idProc) // Verifico que el proceso que abrio el archivo sea el que lo quiere leer
                {
                    Archivo arch = TablaArchivos[posArch]; //busco archivo en la tabla
                    Indicadores indicador = new Indicadores();

                    int inicio = (int)Math.Floor((decimal)(offset) / (decimal)(disp.GetTamBloques())); //bloque donde comienza a leerse
                    int fin = (int)Math.Ceiling((decimal)(offset + cant_uA) / (decimal)(disp.GetTamBloques()));
                   
                    if (fin > arch.getTablaDireccion().Count) // Control de cuanto se quiere leer
                    {
                        throw new Exception("Se quiere leer mas de lo que hay");
                    }
                    switch (GetOrganizacionFisica())
                    {
                        case Org.Contigua:
                        {
                                indicador.tGestionTotal = disp.GetTseek(); //el t de gestion es el t en ir al primer bloque
                                break;
                        }
                        case Org.Enlazada:
                        {
                                 int bloqueLeido = -1;

                                 for (int i=0; i<fin; i++)
                                 {
                                   if (arch.getTablaDireccion()[i] != bloqueLeido - 1) //comprueba si los bloques son contiguos
                                   {
                                       indicador.tGestionTotal += disp.GetTseek();
                                   }
                                   bloqueLeido = arch.getTablaDireccion()[i];
                                 }

                                indicador.tGestionTotal += inicio * disp.GetTlectura();

                             break;
                        }
                        case Org.Indexada:
                        {
                                indicador.tGestionTotal = (2 * disp.GetTseek() + disp.GetTprocesamient()) * (fin - inicio);
                                break;
                        }
                    }

                    indicador.tEspera = tSimulacion - TablaOperaciones[getOpActual()].Tarribo;
                    indicador.tLectoEscritura = (fin - inicio) * disp.GetTlectura();
                    indicador.tSatisfaccion =  indicador.tGestionTotal + indicador.tEspera + indicador.tLectoEscritura;
                    indicador.tipoOp = 'R';
                    
                    tOP = indicador.tSatisfaccion - indicador.tEspera;
                    
                    indicadoresOP.Add(indicador);
                }
                else // Si el archivo esta abierto x otro proceso --> lo agrego a la cola de espera
                {
                    if (!Find(ColaEspera, GetContadorOp()))
                    {
                        ColaEspera.Add(GetContadorOp()); // Si no es asi, lo agrego a la cola de espera
                    }
                }
            }
            return tOP;
        }

        public int Delete(string nameArchivo)
        {
            int numBloque = 0;
            int tOP = 0;
            int posArch = BuscaArch(nameArchivo); //busca si existe el arhivo
  
            // Corroboro que el archivo se encuentre en la tabla (por nombre) y que se encuentre cerrado
            if ( posArch != -1 && TablaArchivos[posArch].getEstado() == -1) //Estado -1 significa que el archivo est� cerrado
            {
                // Dejo cada bloque como libre
                for (int j=0; j<TablaArchivos[posArch].getTablaDireccion().Count; j++)
                {
                    numBloque = TablaArchivos[posArch].getTablaDireccion()[j];
                    disp.CambiarEstadoOcupado(numBloque, 0);

                    if (organizacionFisica == Org.Contigua)
                    {
                        disp.CambiarEstadoBurocracia(numBloque, 0);
                    }
                    else
                    {
                        disp.CambiarEstadoBurocracia(numBloque, 0);
                    }

                    disp.CambiarEstadoReserva(numBloque, false);
                }

                Indicadores indicador = new Indicadores();

                indicador.tLectoEscritura = 0;
                indicador.tGestionTotal = disp.GetTprocesamient();
                indicador.tEspera = tSimulacion - TablaOperaciones[getOpActual()].Tarribo;
                indicador.tSatisfaccion = indicador.tLectoEscritura + indicador.tEspera + indicador.tGestionTotal;
                indicador.tipoOp = 'D';

                indicadoresOP.Add(indicador);

                tOP = indicador.tLectoEscritura + indicador.tGestionTotal;
                // Lo quito de la tabla de archivos
                TablaArchivos.RemoveAt(posArch);
            }
            else if (TablaArchivos[posArch].getEstado() != -1) // Si el archivo se encuentra abierto por algun proceso, lo agrego a la cola de espera
            {
                if (!Find(ColaEspera, GetContadorOp()))
                {
                    ColaEspera.Add(GetContadorOp()); // Si no es asi, lo agrego a la cola de espera
                }
            }

            return tOP;
        }

        public int Open(string nameArchivo, int processID)
        {
            int posArch = BuscaArch(nameArchivo);
            int tOP = 0;

            // Si el archivo se encuentra en la tabla, y si el estado es -1, lo abro cambiando el numero de estado por 
            // el numero de proceso que lo quiere abrir.
            if (posArch != -1 && TablaArchivos[posArch].getEstado() == -1)
            {
                Indicadores indicador = new Indicadores();

                indicador.tGestionTotal = disp.GetTprocesamient(); //DUDA lu: no se si esto esta bien!
                indicador.tipoOp = 'O';
                indicador.tEspera = tSimulacion - TablaOperaciones[getOpActual()].Tarribo;
                indicador.tLectoEscritura = 0;
                indicador.tSatisfaccion = indicador.tLectoEscritura + indicador.tGestionTotal + indicador.tEspera;
                tOP = indicador.tLectoEscritura + indicador.tGestionTotal;

                indicadoresOP.Add(indicador);

                TablaArchivos[posArch].setEstado(processID);
                
            }
            else if (TablaArchivos[posArch].getEstado() != -1) // Si el archivo ya se encuentra abierto, agrego esta operacion a la cola de espera
            {
                if (!Find(ColaEspera, GetContadorOp()))
                {
                    ColaEspera.Add(GetContadorOp()); // Si no es asi, lo agrego a la cola de espera
                }
            }

            return tOP;
        }

        public int Close(string nameArchivo, int processID)
        {
            int posArch = BuscaArch(nameArchivo);
            int tOP = 0;

            // Si el archivo se encuentra en la tabla, y si el num de proceso que quiere cerrarlo es el que lo tiene
            // abierto, lo cierro cambiando el estado a -1
            if (posArch != -1 && TablaArchivos[posArch].getEstado() == processID)
            {
                Indicadores indicador = new Indicadores();

                indicador.tGestionTotal = disp.GetTprocesamient(); //DUDA lu: no se si esto esta bien!
                indicador.tipoOp = 'C';
                indicador.tEspera = tSimulacion - TablaOperaciones[getOpActual()].Tarribo;
                indicador.tLectoEscritura = 0;
                indicador.tSatisfaccion = indicador.tEspera + indicador.tLectoEscritura + indicador.tGestionTotal;
                tOP = indicador.tGestionTotal + indicador.tLectoEscritura;

                indicadoresOP.Add(indicador);

                TablaArchivos[posArch].setEstado(-1);
            }
            else if (TablaArchivos[posArch].getEstado() != -processID) // si el archivo lo tiene abierto otro proceso, agrego esta operacion a la cola de espera
            {
                if (!Find(ColaEspera, GetContadorOp()))
                {
                    ColaEspera.Add(GetContadorOp()); // Si no es asi, lo agrego a la cola de espera
                }
            }

            return tOP;
        }
         //bloquesRealocar son los bloques totales!!!! 
        public bool realocar(ref Archivo arch, int BloquesRealocar, int BloquesAntes, ref Indicadores indicador)
        {
            bool aux = false;

            int uArealocar = BloquesRealocar*disp.GetTamBloques();

            if (disp.GetLibres(uArealocar, GetOrganizacionFisica(), ref arch))
            {
                indicador.tGestionTotal = BloquesAntes * (disp.GetTprocesamient() + disp.GetTseek()) + disp.TprocesamientoBloquesLibres(GetAdminEspacio(), uArealocar);
                aux = true;
            }

            return aux;
        }


        // Funci�n que te busca un archivo en la Tabla por el nombre
        public int BuscaArch(string nameArchivo)
        {
            int posArch = -1;

            for (int i = 0; i < TablaArchivos.Count; i++)
            {
                if (nameArchivo == TablaArchivos[i].getNombre())
                {
                    return i;
                }
            }

            return posArch; //Si devuelve -1 es que no est� en la tabla
        }

        public int ObtenerTmax(char tipo) //funcion para obtener el maximo dependiento el tiempo de operacion
        {
            int Tmax = 0;

            for (int i = 0; i < indicadoresOP.Count; i++)
            {
                if (indicadoresOP[i].tipoOp == tipo)
                {
                    if (indicadoresOP[i].tSatisfaccion > Tmax)
                    {
                        Tmax = indicadoresOP[i].tSatisfaccion;
                    }
                }
            }

            return Tmax;
        }

        public int ObtenerTmin(char tipo) //funcion que devuelve el minimo dependiento el tiempo de operacion
        {
            int Tmin = 0;

            for (int i = 0; i < indicadoresOP.Count; i++)
            {
                if (indicadoresOP[i].tipoOp == tipo)
                {
                    if (indicadoresOP[i].tSatisfaccion < Tmin)
                    {
                        Tmin= indicadoresOP[i].tSatisfaccion;
                    }
                }
            }

            return Tmin;
        }

        public bool Find(List<int> cola, int val)
        {
            for (int i = 0; i < cola.Count; i++)
            {
                if (cola[i] == val)
                {
                    return true;
                }
            }
            return false;
        }

        public Dictionary<string, object> Resultados()
        {
            Dictionary<string, object> res = new Dictionary<string, object>();

            float fragInt = 0;
            float fragExt = 0;
            int datos = 0;
            int metadatos = 0;
            int aux1 = 0;
            int cnt = 0;

            float porDatos;
            float porMetadatos;
            float tiempoGestion = 0;
            int tMaxC, tMaxO, tMaxN, tMaxD, tMaxR, tMaxW;
            int tMinC, tMinO, tMinN, tMinD, tMinR, tMinW;
            float tEsperaProm = 0;


            // Fragmentacion Interna es la cantidad de espacio libre en los bloques reservados, dividido la cantidad total de bloques reservados 
            disp.getFragInt(ref aux1, ref cnt);
            fragInt = aux1 / (aux1 + cnt);

            // Fragmentacion Externa es la cantidad de espacio libre en uA, dividido el tama�o total del dispositivo en uA
            fragExt = (disp.getFragExt() * disp.GetTamBloques()) / disp.GetTamDispositivo();

            disp.datosMetadatos(ref datos, ref metadatos);

            // Porcentaje de Datos es la cantidad de uA de datos dividido la cantidad de uA ocuapadas total
            porDatos = (float)datos / (datos + metadatos);

            // Porcentaje de Metadatos es la cantidad de uA de metadatos dividido la cantidad de uA ocupadas total
            porMetadatos = (float)metadatos / (datos + metadatos);
            
            for (int i = 0; i < indicadoresOP.Count; i++)
            {
                tiempoGestion += indicadoresOP[i].tGestionTotal;
            }

            // El % de tiempo consumido en gestion es el tiempo de gestion total dividido el tiempo total de simulacion
            tiempoGestion = tiempoGestion / tSimulacion;

            // Calculos tMax y tMin para cada tipo de operacion
            tMaxC = ObtenerTmax('C');
            tMinC = ObtenerTmin('C');
            tMaxD = ObtenerTmax('D');
            tMinD = ObtenerTmin('D');
            tMaxN = ObtenerTmax('N');
            tMinN = ObtenerTmin('N');
            tMaxO = ObtenerTmax('O');
            tMinO = ObtenerTmin('O');
            tMaxR = ObtenerTmax('R');
            tMinR = ObtenerTmin('R');
            tMaxW = ObtenerTmax('W');
            tMinW = ObtenerTmin('W');

            for (int i=0; i<indicadoresOP.Count; i++)
            {
                tEsperaProm += indicadoresOP[i].tEspera;
            }

            tEsperaProm = tEsperaProm / indicadoresOP.Count;

            // -------- Indicadores del Sistema --------
            res.Add("fragInt", fragInt);
            res.Add("fragExt", fragExt);
            res.Add("%datos", porDatos);
            res.Add("%metadatos", porMetadatos);
            res.Add("tGestion", tiempoGestion);
            res.Add("tEspera", tEsperaProm);
            res.Add("tMaxC", tMaxC);
            res.Add("tMinC", tMinC);
            res.Add("tMaxO", tMaxO);
            res.Add("tMinO", tMinO);
            res.Add("tMaxN", tMaxN);
            res.Add("tMinN", tMinN);
            res.Add("tMaxD", tMaxD);
            res.Add("tMinD", tMinD);
            res.Add("tMaxR", tMaxR);
            res.Add("tMinR", tMinR);
            res.Add("tMaxW", tMaxW);
            res.Add("tMinW", tMinW);
            res.Add("tSimulacion", tSimulacion);

            // -------- Indicadores por Operacion --------
            res.Add("IndicadoresOP", indicadoresOP);


            return res;
        }

        /**
         * Getters y Setters
        **/
        // @ Ayrton, llama a este para indicar que operacion se esta ejecutando en la interfaz
        public int getOpActual()
        {
            return this.opActual;
        }


        public int GetCantidadOp()
        {
            return this.TablaOperaciones.Count;
        }

        public int GetContadorOp()
        {
            return this.ContadorOp;
        }

        public void SetContadorOp(int value)
        {
            ContadorOp = value;
        }

        public Operacion GetOperacion() //devuelve la operacion actual?
        {
            return (Operacion)this.TablaOperaciones[this.ContadorOp];
        }

        public Dispositivo GetDispositivo()
        {
            return this.disp;
        }

        //Una vez cargada la configuracion vamos a permitir cambiarla, porq esto significa que
        //vamos a tener que modificar al dispositivo tambien (no solo en el constructor de FileSim)!!!
        // No entendi (Fede)
        public Org GetOrganizacionFisica()
        {
            return organizacionFisica;
        }

        public void SetOrganizacionFisica(Org value)
        {
            organizacionFisica = value;
        }

        public Libres GetAdminEspacio()
        {
            return adminEspacio;
        }

        public void SetAdminEspacio(Libres value)
        {
            switch(value)
            {
                case Libres.MapadeBits:
                    {
                        // Reservo un bloque al final para el mapa de bits, y lo marco lleno de uA de Burocracia
                        disp.CambiarEstadoReserva(disp.GetCantBloques() - 1, true);
                        disp.CambiarEstadoBurocracia(disp.GetCantBloques() - 1, disp.GetTamBloques());
                        break;
                    }
                case Libres.ListadeLibres:
                    {
                        for (int i= 0; i < disp.GetCantBloques() - 1; i++)
                        {
                            disp.CambiarEstadoBurocracia(i,1); //le asignamos 1 uA que es el indice
                        }

                        break;
                    }
                case Libres.ListadeLibresdePrincipioyCuenta:
                    {
                        // Reservo 2 bloques al final para la lista y lo marco lleno de uA de Burocracia
                        disp.CambiarEstadoReserva(disp.GetCantBloques() - 2, true);
                        disp.CambiarEstadoBurocracia(disp.GetCantBloques() - 2, disp.GetTamBloques());
                        disp.CambiarEstadoReserva(disp.GetCantBloques() - 1, true);
                        disp.CambiarEstadoBurocracia(disp.GetCantBloques() - 1, disp.GetTamBloques());

                        break;
                    }
            }
            adminEspacio = value;
        }

        public Acceso GetMetodoAcceso()
        {
            return metodoAcceso;
        }

        public void SetMetodoAcceso(Acceso value)
        {
            metodoAcceso = value;
        }

        public Bloque[] getTablaBloques()
        {
            return disp.getTablaBloques();
        }

        public List<Operacion> getTablaOperaciones()
        {
            return TablaOperaciones;
        }

        
    }
}


public class ComparaOp : IComparer<Operacion>
{
    public int Compare(Operacion x, Operacion y)
    {
        if (x.Tarribo < y.Tarribo)
        {
            return -1;
        }
        else if (x.Tarribo == y.Tarribo)
        {
            return 0;
        }
        else
        {
            return 1; 
        }
    }
}
