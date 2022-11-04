﻿using Domain;
using FluentValidation;
using MediatR;
using Persistence;
using Application.Core;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;

namespace Application.Activities
{
    public class EditSeries
    {
        public class Command : IRequest<Result<Unit>>
        {
            public Activity Activity
            {
                get;
                set;
            }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(x => x.Activity).SetValidator(new ActivityValidator());
            }
        }

        public class Handler : IRequestHandler<Command, Result<Unit>>
        {
            private readonly DataContext _context;
            private readonly IMapper _mapper;
            private readonly IConfiguration _config;

            public Handler(DataContext context, IMapper mapper, IConfiguration config)
            {
                _context = context;
                _mapper = mapper;
                _config = config;
            }

            public async Task<Result<Unit>> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    Helper.InitHelper(_mapper);
                    var activitiesToBeDeleted = _context.Activities.Where(x => x.RecurrenceId == request.Activity.RecurrenceId);
                    var newActivities = new List<Activity>();
              
                    Settings s = new Settings();
                    var settings = s.LoadSettings(_config);
                    GraphHelper.InitializeGraph(settings, (info, cancel) => Task.FromResult(0));

                 if (
                  string.IsNullOrEmpty(request.Activity.CoordinatorEmail) &&
                  (
                  request.Activity.RoomEmails.Any() ||
                  !string.IsNullOrEmpty(request.Activity.EventLookup)
                  )
                 )
                    {
                        request.Activity.CoordinatorEmail = GraphHelper.GetEEMServiceAccount();
                        request.Activity.CoordinatorFirstName = "EEMServiceAccount";
                        request.Activity.CoordinatorLastName = "EEMServiceAccount";
                    }

                    //delete the old activities and the recurrence we will make new ones
                    foreach (var item in activitiesToBeDeleted)
                    {
                        if (
                            !string.IsNullOrEmpty(request.Activity.EventLookup) &&
                            !string.IsNullOrEmpty(request.Activity.CoordinatorEmail)
                            )
                        {
                            // delete graph event we will make new ones
                            await GraphHelper.DeleteEvent(item.EventLookup, item.CoordinatorEmail);
                        }
                    }
                   
                 
                    _context.Activities.RemoveRange(activitiesToBeDeleted);
                    await _context.SaveChangesAsync();
                    var oldRecurrence = await _context.Recurrences.FirstOrDefaultAsync(x => x.Id == request.Activity.RecurrenceId);
                    _context.Recurrences.Remove(oldRecurrence);
                    await _context.SaveChangesAsync();

                    // create the new activities


                    Recurrence newRecurrence = new Recurrence();
                        _mapper.Map(request.Activity.Recurrence, newRecurrence);
                        newActivities = Helper.GetActivitiesFromRecurrence(newRecurrence, request.Activity);
            
                 

                    foreach (var a in newActivities.ToList())
                    {
                        if (request.Activity.RecurrenceInd && request.Activity.Recurrence != null)
                        {
                            a.RecurrenceId = request.Activity.Recurrence.Id;
                        }

                        if (!string.IsNullOrEmpty(a.CoordinatorEmail))
                        {
                            //create outlook event
                            GraphEventDTO graphEventDTO = new GraphEventDTO
                            {
                                EventTitle = a.Title,
                                EventDescription = a.Description,
                                Start = a.StartDateAsString,
                                End = a.EndDateAsString,
                                RoomEmails = a.RoomEmails,
                                RequesterEmail = a.CoordinatorEmail,
                                RequesterFirstName = a.CoordinatorFirstName,
                                RequesterLastName = a.CoordinatorLastName,
                                IsAllDay = a.AllDayEvent
                            };
                            Event evt = await GraphHelper.CreateEvent(graphEventDTO);
                            a.EventLookup = evt.Id;
                        }

                        newActivities.Add(a);
                    }
                    newRecurrence.Activities = newActivities;   
                    _context.Recurrences.Add(newRecurrence);
                    var result = await _context.SaveChangesAsync() > 0;
                    if (!result) return Result<Unit>.Failure("Failed to Update Activity");

                    return Result<Unit>.Success(Unit.Value);
                }
                catch (Exception e)
                {

                    throw;
                }
            }

        }

    }

}