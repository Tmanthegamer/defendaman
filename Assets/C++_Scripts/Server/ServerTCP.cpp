#include "ServerTCP.h"

using namespace Networking;
using namespace json11;

/**
 * Initialize server socket and address
 * @author Jerry Jia
 * @date   2016-03-11
 * @param  port       port number
 * @return            -1 on failure, 0 on success
 */
int ServerTCP::InitializeSocket(short port)
{
    int err = -1;
	  int optval = 1;	/* set SO_REUSEADDR on a socket to true (1) */

    /* Create a TCP streaming socket */
    if ((_TCPAcceptingSocket = socket(AF_INET, SOCK_STREAM, 0)) == -1 )
    {
        fatal("InitializeSocket: socket() failed\n");
        return _TCPAcceptingSocket;
    }

    /* Allows other sockets to bind() to this port, unless there is an active listening socket bound to the port already. */
	  setsockopt(_TCPAcceptingSocket, SOL_SOCKET, SO_REUSEADDR, &optval, sizeof(optval));

    /* Fill in server address information */
    memset(&_ServerAddress, 0, sizeof(struct sockaddr_in));
    _ServerAddress.sin_family = AF_INET;
    _ServerAddress.sin_port = htons(port);
    _ServerAddress.sin_addr.s_addr = htonl(INADDR_ANY); // Accept connections from any client

    /* bind server address to accepting socket */
    if ((err = bind(_TCPAcceptingSocket, (struct sockaddr *)&_ServerAddress, sizeof(_ServerAddress))) == -1)
    {
        std::cout << "InitializeSocket: bind() failed with errno " << errno << std::endl;
        return err;
    }
    /* Listen for connections */
    listen(_TCPAcceptingSocket, MAXCONNECTIONS);

    return 0;
}

/**
 * Calls accept on a player's socket. Sets the returning socket and client address structure to the player.
 * Add connected player to the list of players
 * @author Jerry Jia, Martin Minkov
 * @date   2016-03-11
 * @param  player     player object
 * @return            -1 on failure, 0 on success
 */
int ServerTCP::Accept(Player * player)
{
    unsigned int        ClientLen = sizeof(player->tcp_connection);

    /* Accepts a connection from the client */
    if ((player->socket = accept(_TCPAcceptingSocket, (struct sockaddr *)&player->tcp_connection, &ClientLen)) == -1)
    {
        std::cerr << "Accept() failed with errno" << errno << std::endl;
        return -1;
    }

    player->isReady = false;
    player->playerClass = 0;
    player->team = 0;

    //Add player to list
    int id = getPlayerId(inet_ntoa(player->tcp_connection.sin_addr));
    std::cout << "id:" << id << std::endl;
    player->id = id;
    _PlayerTable->insert(std::pair<int, Player>(id, *player));

    std::string temp(inet_ntoa(player->tcp_connection.sin_addr));
    _Connections->push_back(temp);

    newPlayer = *player;
    return player->id;
}

/**
 * Static function used by client_library.cpp to create a reading thread to handle one client
 * @author Jerry Jia
 * @date   2016-03-11
 * @param  server     ServerTCP object
 * @return            ServerTCP::Receive() address
 */
void * ServerTCP::CreateClientManager(void * server)
{
    return ((ServerTCP *)server)->Receive();
}

/**
 * Continuosly recieves messages from a specific client
 * @author Jerry Jia, Martin Minkov
 * @date   2016-03-11
 * @return 0 for thread execution code
 */
void * ServerTCP::Receive()
{
    Player tmpPlayer = newPlayer;
  	int BytesRead;
    char * buf;						          	/* buffer read from one recv call      	  */

  	buf = (char *)malloc(PACKETLEN); 	/* allocates memory 							        */
    memset(buf, 0, PACKETLEN);
    while (1)
    {
      int bytesToRead = PACKETLEN;
      char *bp = buf;
       while((BytesRead = recv(tmpPlayer.socket, bp, bytesToRead, 0)) < PACKETLEN)
       {
         if (BytesRead == 0)
          break;

         bytesToRead -= BytesRead;
         bp += BytesRead;
       }

        /* recv() failed */
      	if(BytesRead < 0)
      	{
      		printf("recv() failed with errno: %d", errno);
      		return 0;
      	}
        /* client disconnected */
      	if(BytesRead == 0)
      	{
      		sprintf(buf, "Player %d has left the lobby \n", tmpPlayer.id);
          printf("%s", buf);

          //Send all players that this player has left
          this->ServerTCP::Broadcast(buf);

          //Remove player from player list
          std::cerr << "Player:" << inet_ntoa(tmpPlayer.tcp_connection.sin_addr) << std::endl;
          _Connections->erase(std::remove(_Connections->begin(),
                                          _Connections->end(),
                                          inet_ntoa(tmpPlayer.tcp_connection.sin_addr)),
                             _Connections->end());
          _PlayerTable->erase(tmpPlayer.id);

          return 0;
      	}
        //Handle Data Received
        std::cout << "Received: " << buf << std::endl;
        this->ServerTCP::CheckServerRequest(tmpPlayer, buf);
    }
    free(buf);
    return 0;
}

/*
	Sends a message to all the clients

  @author Jerry Jia, Gabriella Chueng
  @date   2016-03-11
  @param  message    [description]

  Revision:
  Date       Author      Description
  2016-03-10 Gabriel Lee Add functionality to add exception to broadcast
*/
void ServerTCP::Broadcast(const char* message, sockaddr_in * excpt)
{
  Player tmpPlayer;
  for(const auto &pair : (*_PlayerTable))
  {
    tmpPlayer = pair.second;
    if(send(tmpPlayer.socket, message, PACKETLEN, 0) == -1)
    {
      std::cerr << "Broadcast() failed for player id: " << pair.first << std::endl;
			std::cerr << "errno: " << errno << std::endl;
			return;
    }
  }
}

/**
 * Sends a message to a specific client
 * @author Martin Minkov, Scott Plummer
 * @date   2016-03-11
 * @param  player     Player to send
 * @param  message    message to send
 */
void ServerTCP::sendToClient(Player player, const char * message)
{
	if(send(player.socket, message, PACKETLEN, 0) == -1)
	{
		std::cerr << "Broadcast() failed for player id: " << player.id << std::endl;
		std::cerr << "errno: " << errno << std::endl;
		return;
	}
}
/**
 * Parse client json message and determines server logic
 * @author Jerry Jia, Martin Minkov, Scott Plummer, Dylan Blake
 * @date   2016-03-11
 * @param  player     Player that recives the message
 * @param  buffer     json message
 *
 * Revisions: Tyler Trepanier-Bracken  2016/03/14
 *            Added in a GameEnd case with a restart server placeholder.
 */
void ServerTCP::CheckServerRequest(Player player, char * buffer)
{
  std::string error;
  Json json = Json::parse(buffer, error).array_items()[0];

  if (json["DataType"].int_value() != Networking) {
    this->ServerTCP::Broadcast(buffer);
    return;
  }

  switch(json["ID"].int_value())
  {
    //Player joining team request
    case TeamChangeRequest:
      std::cout << "Team change: " << json[TeamID].int_value() << std::endl;
      (*_PlayerTable)[player.id].team = json[TeamID].int_value();
      this->ServerTCP::Broadcast(buffer);
      break;

    //Player joining class request
    case ClassChangeRequest:
      std::cout << "Class change: " << json[ClassID].int_value() << std::endl;
      (*_PlayerTable)[player.id].playerClass = json[ClassID].int_value();
      this->ServerTCP::Broadcast(buffer);
      break;

    //Player making a ready request
    case ReadyRequest:
      std::cout << "Ready change: " << (json[Ready].int_value() ? "ready" : "not ready") << std::endl;
      (*_PlayerTable)[player.id].isReady = (json[Ready].int_value() ? true : false);
      this->ServerTCP::Broadcast(buffer);
      break;

    //New Player has joined lobby
    case PlayerJoinedLobby:
  	  std::cout << "New Player Change: " << json[UserName].string_value() << std::endl;
  	  strcpy((*_PlayerTable)[player.id].username, json[UserName].string_value().c_str());

  	  //Send player a table of players
  	  sendToClient(player, constructPlayerTable().c_str());

      //Create packet and send to everyone
      this->ServerTCP::Broadcast(UpdateID((*_PlayerTable)[player.id]).c_str());
      break;

    case PlayerLeftLobby:
      std::cout << "Player: " << json[PlayerID].int_value() << " has left the lobby" << std::endl;
      _PlayerTable->erase(json[PlayerID].int_value());
      this->ServerTCP::Broadcast(buffer);
      write(_sockPair[0], "2", PACKETLEN);
      break;

    case GameStart:
      std::cout << "Player: " << json[PlayerID].int_value() << " has started the game" << std::endl;
      //All players in lobby are ready
      if (this->ServerTCP::AllPlayersReady())
      {
        this->ServerTCP::Broadcast(buffer);
        this->ServerTCP::Broadcast(generateMapSeed().c_str());
      }
      break;

    case GameEnd: //Currently allows any player to annouce the end of the game.
      std::cout << "Player: " << json[PlayerID].int_value() << " has ended the game" << std::endl;
      this->ServerTCP::Broadcast(buffer);     // Inform all clients that the game has ended.
      this->ServerTCP::ShutDownGameServer();  // Send a message to the UDP to kill itself.
      break;
  }
}


/**
 * Check if all the players within _ClientTable are ready
 * @author ???
 * @date   2016-03-11
 * @return true if all the players are ready, false otherwise
 */
bool ServerTCP::AllPlayersReady()
{
  Player tmpPlayer;
  for(const auto &pair : (*_PlayerTable))
  {
    tmpPlayer = pair.second;
    if(tmpPlayer.isReady == false)
    {
      printf("Player %d is not ready\n", tmpPlayer.id);
			return false;
    } else {
      printf("Player %d is ready\n", tmpPlayer.id);
    }
  }
  return true;
}

/**
 * Constructs a json message containing an array of current player's statuses
 * @author Martin Minkov, Scott Plummer, Jerry Jia
 * @date   2016-03-11
 * @return the constructed json table
 */
std::string ServerTCP::constructPlayerTable()
{
	std::string packet = "[{\"DataType\" : 6, \"ID\" : 6, \"LobbyData\" : [";
	for (auto it = _PlayerTable->begin(); it != _PlayerTable->end();)
	{
		std::string tempUserName((it->second).username);
		packet += "{";
    packet += "\"PlayerID\" : " + std::to_string(it->first);
		packet += ",  \"UserName\" : \"" + tempUserName + "\"";
		packet += ", \"TeamID\": " +  std::to_string((it->second).team);
		packet += ", \"ClassID\" : " + std::to_string((it->second).playerClass);
		packet += ", \"Ready\" : " + std::to_string(Server::isReadyToInt(it->second));
		packet += (++it == _PlayerTable->end() ? "}" : "},");
	}
	packet +=    "]";
  packet += "}]";

  std::cout << "THIS IS OUR PACKET THAT WE ARE SENDING" << packet << std::endl;
	return packet;
}

/**
 * Returns the registered player list from the game lobby
 * @author Martin Minkov, Scott Plummer
 * @date   2016-03-11
 * @param  player     player object
 * @return            updated json with the player's id
 */
std::string ServerTCP::UpdateID(const Player& player)
{
   char buf[PACKETLEN];
   std::cout << player.username << std::endl;
   sprintf(buf, "[{\"DataType\" : 6, \"ID\" : 4, \"PlayerID\" : %d, \"UserName\" : \"%s\"}]", player.id, player.username);
   std::string temp(buf);
   return temp;
}

/**
 * [ServerTCP::generateMapSeed description]
 * @author ???
 * @date   2016-03-11
 * @return [description]
 */
std::string ServerTCP::generateMapSeed()
{
	int mapSeed;
	srand (time (NULL));
	mapSeed = rand ();
	std::string packet = "[{\"DataType\" : 3, \"ID\" : 0, \"Seed\" : " + std::to_string(mapSeed) +"}]";
	return packet;
}

/**
 * ???
 */
std::map<int, Player> ServerTCP::getPlayerTable()
{
  return (*_PlayerTable);
}

/**
 * [ServerTCP::ShutDownGameServer                                     ]
 * [    Sends a message to the UDP Server via the already created pipe]
 * [    indicating that the game is over.                             ]
 * @author Tyler Trepanier-Bracken
 * @date   2016-03-14
 * @param  void
 * @return void
 */
void ServerTCP::ShutDownGameServer(void)
{
  char sbuf[20];
  char rbuf[20];
  static int MESSAGE_SIZE = 20;
  int nread = 0;
  sprintf(sbuf, "%s", "GameEnd");

  if(_sockPair[0] == -1 && _sockPair[1] == -1) //check if the sockets aren't in error
  {
    // Critical error SIGTERM the UDP
    // TODO
  }

  for (;;) // Wait forever until a read has occured.
  {
    switch (nread = read(_sockPair[0], &rbuf, MESSAGE_SIZE)) // Polling, need to fix
    {
      case -1:
      case 0:
        break;
      default:
        // Assume that any message means that the UDP is shutting down
        std::cerr << "UDP shutting down." << std::endl;
    }
  }
}
