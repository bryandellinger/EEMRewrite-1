﻿using Application.Core;
using MediatR;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Application.GraphSchedules
{
    public class EventsByRoom
    {
        public class Query : IRequest<Result<List<FullCalendarEventDTO>>>
        {
            public string Id { get; set; }
            public string Start { get; set; }
            public string End { get; set; }
        }

        public class Handler : IRequestHandler<Query, Result<List<FullCalendarEventDTO>>>
        {
            private readonly IConfiguration _config;
            public Handler(IConfiguration config)
            {
                _config = config;
            }

            public async Task<Result<List<FullCalendarEventDTO>>> Handle(Query request, CancellationToken cancellationToken)
            {
                Settings s = new Settings();
                var settings = s.LoadSettings(_config);
                GraphHelper.InitializeGraph(settings, (info, cancel) => Task.FromResult(0));
                var allRooms = await GraphHelper.GetRoomsAsync();
                var room = allRooms.Where(r => r.Id == request.Id).FirstOrDefault();
                string email = room.AdditionalData["emailAddress"].ToString();
                var startArray = request.Start.Split('T');
                var endArray = request.End.Split('T');
                var startDateArray = startArray[0].Split('-');
                var endDateArray = endArray[0].Split('-');
                int startMonth = Int32.Parse(startDateArray[1]);
                int endMonth = Int32.Parse(endDateArray[1]);
                int startDay = Int32.Parse(startDateArray[2]);
                int endDay = Int32.Parse(endDateArray[2]);
                int startYear = Int32.Parse(startDateArray[0]);
                int endYear = Int32.Parse(endDateArray[0]);
                DateTime startDateTime = new DateTime(startYear, startMonth, startDay, 0, 0, 0 );
                DateTime endDateTime = new DateTime(endYear, endMonth, endDay, 23, 59, 0);
                string startDateAsString = startDateTime.ToString("o", CultureInfo.InvariantCulture);
                string endDateAsString = endDateTime.ToString("o", CultureInfo.InvariantCulture);
                DateTimeTimeZone startDateTimeZone = new DateTimeTimeZone();
                DateTimeTimeZone endDateTimeZone = new DateTimeTimeZone();
                startDateTimeZone.DateTime = startDateAsString;
                startDateTimeZone.TimeZone = "Eastern Standard Time";
                endDateTimeZone.DateTime = endDateAsString;
                endDateTimeZone.TimeZone = "Eastern Standard Time";

                ScheduleRequestDTO scheduleRequestDTO = new ScheduleRequestDTO
                {
                    Schedules = new List<string> { email },
                    StartTime = startDateTimeZone,
                    EndTime = endDateTimeZone,  
                    AvailabilityViewInterval = 15
                };

                ICalendarGetScheduleCollectionPage result = await GraphHelper.GetScheduleAsync(scheduleRequestDTO);
                List<FullCalendarEventDTO> fullCalendarEventDTOs = new List<FullCalendarEventDTO>();
              
                foreach (ScheduleInformation scheduleInformation in result.CurrentPage)
                {
                    foreach (ScheduleItem scheduleItem  in scheduleInformation.ScheduleItems)
                    {
                        if (scheduleItem.Status != FreeBusyStatus.Free)
                        {
                            FullCalendarEventDTO fullCalendarEventDto = new FullCalendarEventDTO
                            {
                                Id = Guid.NewGuid().ToString(),
                                Start = scheduleItem.Start.DateTime,
                                End = scheduleItem.End.DateTime,
                                Title = scheduleItem.Subject,
                                Color = scheduleItem.Status == FreeBusyStatus.Busy ? "Green" : "GoldenRod",
                                AllDay = scheduleItem.Start.DateTime.Split('T')[1] == "00:00:00.0000000" &&
                                         scheduleItem.End.DateTime.Split('T')[1] == "00:00:00.0000000" ? true : false
                            };
                            fullCalendarEventDTOs.Add(fullCalendarEventDto);
                        }
                        
                    }
                }
                return Result<List<FullCalendarEventDTO>>.Success(fullCalendarEventDTOs);
            }
        }
    }
}
