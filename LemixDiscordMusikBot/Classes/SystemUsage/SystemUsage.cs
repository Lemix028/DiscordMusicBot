namespace LemixDiscordMusikBot
{
    class SystemUsage
    {
        double CPU;
        double RAM;
        public SystemUsage(double CPU, double RAM)
        {
            this.CPU = CPU;
            this.RAM = RAM;
        }

        public double getCPU()
        {
            return CPU; 
        }

        public double getRAM()
        {
            return RAM;
        }

    }
}
