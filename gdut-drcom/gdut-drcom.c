#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "getopt.h"
#include "auth.h"
#include "config.h"

static void print_help(char * name);

int main()
{
	const char *config = ".\\authCfg.txt";
	parse_config(config);

#ifdef DEBUG
	fprintf(stdout, "drcom_config.remote_ip = %s\n", drcom_config.remote_ip);
	fprintf(stdout, "drcom_config.remote_port = %d\n", drcom_config.remote_port);
	fprintf(stdout, "drcom_config.keep_alive1_flag = %02hhx\n", drcom_config.keep_alive1_flag);
	fflush(stdout);
#endif

	auth();

	return 0;
}

static void print_help(char *name)
{
//    fprintf(stdout, "gdut-drcom\n");
	fprintf(stdout,
		"                   __     __         __                 \n"
		"          ___  ___/ /_ __/ /_    ___/ /__________  __ _ \n"
		"         / _ `/ _  / // / __/   / _  / __/ __/ _ \\/  ' \\\n"
		"         \\_, /\\_,_/\\_,_/\\__/    \\_,_/_/  \\__/\\___/_/_/_/\n"
		"        /___/   "
			);

	fprintf(stdout, "A third-party drcom client for gdut.\n");
	fprintf(stdout, "usage:\n");
	fprintf(stdout, "  %s\n", name);
	fprintf(stdout, "    --remote-ip <ip addr>               The server ip.\n");
	fprintf(stdout, "\n");
	fprintf(stdout, "    [--remote-port <port>]              The server port, default as 61440.\n");
	fprintf(stdout, "    [--keep-alive1-flag <flag>]         The keep alive 1 packet's flag.\n"
					"                                            default as 00.\n");
	fprintf(stdout, "    [-c, --config-file <file>]          The path to config file.\n");
	fprintf(stdout, "    [-h, --help]                        Print this message.\n");
}

